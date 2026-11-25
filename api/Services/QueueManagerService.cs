using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Hubs;
using XlightsQueue.Models;

namespace XlightsQueue.Services;

public enum QueueManagerState { Unknown, Idle, PlayingDefault, PlayingQueue, Error }
public enum SyncStatus { Unknown, InSync, Drift, Error }

public class QueueManagerService(
    IDbContextFactory<AppDbContext> dbFactory,
    FppService fppService,
    QueueService queueService,
    MqttService mqttService,
    IHubContext<ShowHub> hub,
    ILogger<QueueManagerService> logger)
    : BackgroundService, IQueueTrigger {
    private readonly Channel<bool> _wakeupChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private QueueManagerState _state = QueueManagerState.Unknown;
    private SyncStatus _syncStatus = SyncStatus.Unknown;
    private int? _activeQueueItemId;
    private string? _lastBroadcastTrack;
    private DateTime _lastCommandAt = DateTime.MinValue;
    private const int GraceSeconds = 5;
    // Minimum seconds a song must be in Playing state before we'll trust FPP "idle" as song completion.
    // FPP can take several seconds to load and start a sequence while still reporting idle.
    private const int MinPlayedSeconds = 12;

    // When queue is active we poll faster to catch song transitions quickly
    private const int ActivePollSeconds = 2;
    private const int IdlePollSeconds = 5;

    // Lead-in / lead-out ("warm the moving heads up, turn them off at the end").
    // FPP runs a playlist's lead-in once when it starts and its lead-out once when it ends, but every
    // requested song is its own one-song playlist, so we do it for the whole queue session ourselves.
    // Where the default playlist was when a requested song interrupted it, so it can carry on afterwards.
    private (string Playlist, int Item)? _resume;
    private bool _gracefulPending;                          // FPP was asked to stop after the current song
    private DateTime _lastGracefulAttempt = DateTime.MinValue; // throttles retries if FPP refuses

    // Show-level sequences: once when the show opens, once when it closes.
    private bool? _wasRunning;           // null until the first check, so a restart mid-show doesn't replay the opener
    private bool _openPending, _closePending;

    private async Task PlayShowSequenceAsync(string sequence, string which) {
        // Use the media file of the matching imported song, if there is one (so the sequence has its audio).
        static string Base(string n) => n.Trim().EndsWith(".fseq", StringComparison.OrdinalIgnoreCase) ? n.Trim()[..^5] : n.Trim();
        string media = string.Empty;
        try {
            await using var db = await dbFactory.CreateDbContextAsync();
            var b = Base(sequence);
            media = (await db.Songs.Where(s => s.Filename.StartsWith(b)).ToListAsync())
                .FirstOrDefault(s => string.Equals(Base(s.Filename), b, StringComparison.OrdinalIgnoreCase))?.MediaFilename ?? string.Empty;
        } catch { /* play without media */ }
        logger.LogInformation("Show {Which}: playing '{Sequence}'", which, sequence);
        await fppService.StartSequenceAsync(sequence, media);
        RecordCommand();
    }

    private string? _rigPlaylist;        // FPP playlist whose lead-in has run and whose lead-out is still owed
    private DateTime? _leadOutDue;       // when that lead-out should run (set once the queue goes quiet)
    private static readonly TimeSpan LeadOutHold = TimeSpan.FromSeconds(60); // so a request right after a song doesn't turn the heads off and on

    /// <summary>Plays the owed lead-out once FPP is idle and its time has come. Returns true if it started one.</summary>
    private async Task<bool> RunLeadOutIfDueAsync(FppStatus fpp, bool now) {
        if (_rigPlaylist == null || !fpp.IsIdle) return false;
        if (now) _leadOutDue = DateTime.UtcNow;
        _leadOutDue ??= DateTime.UtcNow + LeadOutHold;
        if (DateTime.UtcNow < _leadOutDue) return false;

        var name = _rigPlaylist;
        _rigPlaylist = null;
        _leadOutDue = null;
        var (_, leadOut) = await fppService.GetLeadEntriesAsync(name);
        if (leadOut.Count == 0) return false;
        logger.LogInformation("Show over: running lead-out of '{Playlist}'", name);
        var ok = await fppService.StartEntriesAsync(leadOut);
        RecordCommand();
        return ok;
    }

    /// <summary>
    /// When a request interrupts the default playlist, remember which item to come back to: the same
    /// song from its start if it was less than half played, otherwise the next one.
    /// </summary>
    private static (string, int)? WorkOutResume(FppStatus fpp, Models.ShowConfig config, bool nextItem = false) {
        var cp = fpp.current_playlist;
        if (cp == null || string.IsNullOrEmpty(config.DefaultPlaylistName)
            || !string.Equals(cp.playlist, config.DefaultPlaylistName, StringComparison.OrdinalIgnoreCase)) return null;
        if (!int.TryParse(cp.index, out var index) || !int.TryParse(cp.count, out var count) || index < 1 || count < 1) return null;
        var played = int.TryParse(fpp.seconds_played, out var sp) ? sp : 0;
        var remaining = int.TryParse(fpp.seconds_remaining, out var sr) ? sr : 0;
        var next = nextItem || (played + remaining > 0 && played > remaining) ? index + 1 : index;
        return (cp.playlist, next > count ? 1 : next);
    }

    public void TriggerImmediateCheck() => _wakeupChannel.Writer.TryWrite(true);

    public DiagnosticsDto GetDiagnostics() => new(
        _state.ToString(),
        _syncStatus.ToString(),
        _activeQueueItemId,
        fppService.GetCommandLog()
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation("QueueManagerService started");

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await EvaluateStateAsync(stoppingToken);
            } catch (Exception ex) {
                logger.LogError(ex, "QueueManagerService error");
                _state = QueueManagerState.Error;
                _syncStatus = SyncStatus.Error;
            }

            // Poll faster when queue is active so we catch song transitions within ~2s
            var queueActive = _state == QueueManagerState.PlayingQueue
                || await queueService.HasPendingItemsAsync();
            var pollInterval = queueActive
                ? TimeSpan.FromSeconds(ActivePollSeconds)
                : TimeSpan.FromSeconds((await GetConfigAsync()).FppPollingIntervalSeconds);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(pollInterval);
            try {
                await _wakeupChannel.Reader.ReadAsync(cts.Token);
                logger.LogDebug("QueueManager woken by trigger");
            } catch (OperationCanceledException) { /* normal timeout */ }
        }
    }

    private async Task EvaluateStateAsync(CancellationToken ct) {
        if (DateTime.UtcNow - _lastCommandAt < TimeSpan.FromSeconds(GraceSeconds)) {
            logger.LogDebug("Grace period active, skipping evaluation");
            return;
        }

        var fppStatus = await fppService.GetStatusAsync();
        if (fppStatus == null) {
            SetState(QueueManagerState.Error, SyncStatus.Error);
            return;
        }

        var playingItem = await queueService.GetPlayingItemAsync();
        var nextPending = await queueService.GetNextPendingAsync();

        // Reconcile: if we thought something was playing but FPP is now idle → mark played.
        // Guard: only mark played if the song has been in Playing state long enough to avoid
        // treating FPP's startup latency (a few seconds of idle before sequence loads) as completion.
        var playingSecondsElapsed = playingItem?.StartedAt.HasValue == true
            ? (DateTime.UtcNow - playingItem.StartedAt!.Value).TotalSeconds
            : double.MaxValue;
        if (playingItem != null && fppStatus.IsIdle && playingSecondsElapsed < MinPlayedSeconds)
            logger.LogDebug("FPP idle but '{Title}' only started {Seconds:F0}s ago: likely still loading, skipping played check", playingItem.Song.Title, playingSecondsElapsed);
        if (playingItem != null && fppStatus.IsIdle && playingSecondsElapsed >= MinPlayedSeconds) {
            logger.LogInformation("Queue song '{Title}' finished (played {Seconds:F0}s)", playingItem.Song.Title, playingSecondsElapsed);
            await queueService.MarkPlayedAsync(playingItem.Id);
            _activeQueueItemId = null;
            await BroadcastQueueAsync();
            await mqttService.PublishAsync("nowplaying", new { title = (string?)null, playing = false });
            playingItem = null;
        }

        var config = await GetConfigAsync();

        // Show open / close: fire once on each change, never on the first look after a restart.
        var running = ScheduleHelper.IsRunning(config);
        if (_wasRunning is { } was) {
            if (running && !was) { _openPending = true; _closePending = false; }
            else if (!running && was) { _closePending = true; _openPending = false; _leadOutDue = DateTime.UtcNow; }
        }
        _wasRunning = running;

        if (running && _openPending) {
            if (string.IsNullOrWhiteSpace(config.OpenSequence)) _openPending = false;
            else if (fppStatus.IsIdle && playingItem == null) {
                _openPending = false;
                await PlayShowSequenceAsync(config.OpenSequence, "open");
                return;
            } else if (!fppStatus.IsIdle && fppStatus.current_playlist?.playlist != "xlq_current") {
                _openPending = false; // FPP is already running its own show, don't cut in
            }
        }

        // Outside the season or show hours nothing plays on its own and the public can't queue,
        // but songs requested by friends & family passes still play (that's how you preview the show).
        if (!ScheduleHelper.IsRunning(config)) {
            nextPending = await queueService.GetNextPendingAsync(guestOnly: true);
            if (nextPending == null) {
                // Stop the default playlist if it's looping; never interrupt a song someone requested.
                if (fppStatus.IsPlayingPlaylist && playingItem == null && fppStatus.current_playlist?.playlist != "xlq_current") {
                    logger.LogInformation(ScheduleHelper.IsSeasonActive(config) ? "Outside show hours: stopping default playlist" : "Season inactive: stopping default playlist");
                    await fppService.StopPlaylistAsync();
                    RecordCommand();
                    _leadOutDue = DateTime.UtcNow; // the show just ended: run its lead-out straight away
                    _resume = null;                // and the next show starts from the top
                } else if (playingItem == null && await RunLeadOutIfDueAsync(fppStatus, false)) {
                    return;
                } else if (_closePending && playingItem == null && fppStatus.IsIdle && _rigPlaylist == null) {
                    _closePending = false; // everything else (default playlist, lead-out) has finished
                    if (!string.IsNullOrWhiteSpace(config.CloseSequence)) {
                        await PlayShowSequenceAsync(config.CloseSequence, "close");
                        return;
                    }
                }
                SetState(playingItem != null ? QueueManagerState.PlayingQueue : QueueManagerState.Idle, SyncStatus.InSync);
                return;
            }
        }

        if (nextPending != null) {
            // FPP is playing something we didn't queue (default playlist or standalone sequence) → stop it.
            // Never stop xlq_current, that's our own temp playlist started by this service.
            var fppPlayingOurPlaylist = fppStatus.current_playlist?.playlist == "xlq_current";
            var fppPlayingUnqueued = (fppStatus.IsPlayingPlaylist || fppStatus.IsPlayingSequence)
                && playingItem == null && !fppPlayingOurPlaylist;
            if (fppPlayingUnqueued && config.InterruptForUserSongs) {
                logger.LogInformation("Stopping FPP playback to play queue song '{Title}'", nextPending.Song.Title);
                _resume = WorkOutResume(fppStatus, config);
                await fppService.StopPlaylistAsync();
                RecordCommand();
                return;
            }

            // Not interrupting: let the current song finish, then FPP ends the playlist (running its
            // lead-out) and the request plays. Without this a looping default playlist never goes idle.
            if (fppPlayingUnqueued && !config.InterruptForUserSongs && fppStatus.IsPlayingPlaylist
                && !_gracefulPending && DateTime.UtcNow - _lastGracefulAttempt > TimeSpan.FromSeconds(30)) {
                logger.LogInformation("Request '{Title}' is waiting: asking FPP to stop after the current song", nextPending.Song.Title);
                var resume = WorkOutResume(fppStatus, config, nextItem: true);
                if (await fppService.StopGracefullyAsync()) {
                    _gracefulPending = true;
                    _resume = resume;                 // come back to the song after the one that finishes
                    _rigPlaylist = null;              // FPP runs this playlist's own lead-out as it ends
                }
                _lastGracefulAttempt = DateTime.UtcNow;
                RecordCommand();
                return;
            }

            if (fppStatus.IsIdle) {
                logger.LogInformation("Starting queue song '{Title}'", nextPending.Song.Title);

                // Show start: the first song of a session (or of a different playlist) gets that
                // playlist's lead-in in front of it. Switching playlists first plays the old lead-out.
                var songList = nextPending.Song.Playlist?.FppPlaylistName;
                List<System.Text.Json.JsonElement>? leadIn = null;
                if (!string.IsNullOrEmpty(songList) && !string.Equals(songList, _rigPlaylist, StringComparison.OrdinalIgnoreCase)) {
                    leadIn = [];
                    if (_rigPlaylist != null) leadIn.AddRange((await fppService.GetLeadEntriesAsync(_rigPlaylist)).LeadOut);
                    leadIn.AddRange((await fppService.GetLeadEntriesAsync(songList)).LeadIn);
                    _rigPlaylist = songList;
                }
                _leadOutDue = null;

                await fppService.StartSequenceAsync(nextPending.Song.Filename, nextPending.Song.MediaFilename, leadIn);
                RecordCommand();
                await queueService.MarkPlayingAsync(nextPending.Id);
                _activeQueueItemId = nextPending.Id;
                SetState(QueueManagerState.PlayingQueue, SyncStatus.InSync);
                await BroadcastQueueAsync();
                await BroadcastNowPlayingAsync(nextPending.Song, fppStatus);
                await mqttService.PublishAsync("nowplaying", new { title = nextPending.Song.Title, artist = nextPending.Song.Artist, playing = true });
                return;
            }

            if (fppStatus.IsPlayingSequence && playingItem != null) {
                var inSync = string.Equals(fppStatus.current_sequence.Trim(), playingItem.Song.Filename.Trim(), StringComparison.OrdinalIgnoreCase);
                _syncStatus = inSync ? SyncStatus.InSync : SyncStatus.Drift;
                SetState(QueueManagerState.PlayingQueue, _syncStatus);
            }
        } else {
            // Queue is empty. FPP runs the default playlist's own lead-out when it ends by itself.
            if (_state == QueueManagerState.PlayingDefault && fppStatus.IsIdle) _rigPlaylist = null;

            var willAutoplay = fppStatus.IsIdle && config.AutoPlayDefault && !string.IsNullOrEmpty(config.DefaultPlaylistName);
            if (willAutoplay && string.Equals(_rigPlaylist, config.DefaultPlaylistName, StringComparison.OrdinalIgnoreCase)) {
                _rigPlaylist = null; // same playlist: FPP's own lead-in runs when it starts
            } else if (willAutoplay && _rigPlaylist != null) {
                if (await RunLeadOutIfDueAsync(fppStatus, true)) return; // end the requests' show, then the default starts
            } else if (!willAutoplay && playingItem == null && await RunLeadOutIfDueAsync(fppStatus, false)) {
                return;
            }

            if (willAutoplay) {
                _rigPlaylist = config.DefaultPlaylistName;
                _leadOutDue = null;
                if (_resume is { } r && string.Equals(r.Playlist, config.DefaultPlaylistName, StringComparison.OrdinalIgnoreCase)) {
                    logger.LogInformation("Resuming default playlist '{Name}' at item {Item}", config.DefaultPlaylistName, r.Item);
                    await fppService.StartPlaylistAtItemAsync(config.DefaultPlaylistName, r.Item);
                } else {
                    logger.LogInformation("Starting default playlist '{Name}'", config.DefaultPlaylistName);
                    await fppService.StartPlaylistAsync(config.DefaultPlaylistName);
                }
                _resume = null;
                RecordCommand();
                SetState(QueueManagerState.PlayingDefault, SyncStatus.InSync);
            } else if (fppStatus.IsPlayingPlaylist && fppStatus.current_playlist?.playlist != "xlq_current")
                SetState(QueueManagerState.PlayingDefault, SyncStatus.InSync);
            else if (fppStatus.IsIdle)
                SetState(QueueManagerState.Idle, SyncStatus.InSync);
        }

        // Broadcast NowPlayingChanged whenever the track changes
        var currentTrack = fppStatus.CurrentTrackName;
        if (currentTrack != _lastBroadcastTrack) {
            _lastBroadcastTrack = currentTrack;
            if (playingItem != null)
                await BroadcastNowPlayingAsync(playingItem.Song, fppStatus);
            else
                await BroadcastFppNowPlayingAsync(fppStatus);
        }
    }

    private void SetState(QueueManagerState state, SyncStatus sync) {
        _state = state;
        _syncStatus = sync;
    }

    private void RecordCommand() => _lastCommandAt = DateTime.UtcNow;

    private async Task BroadcastQueueAsync() {
        var queue = await queueService.GetQueueAsync();
        await hub.Clients.All.SendAsync("QueueUpdated", queue);
    }

    private async Task BroadcastNowPlayingAsync(Song song, FppStatus fppStatus) {
        var played = int.TryParse(fppStatus.seconds_played, out var sp) ? sp : 0;
        var remaining = int.TryParse(fppStatus.seconds_remaining, out var sr) ? sr : 0;
        var dto = new NowPlayingDto(song.Title, song.Artist, song.Filename, DateTime.UtcNow, true, _state.ToString(), played, remaining, song.Id);
        await hub.Clients.All.SendAsync("NowPlayingChanged", dto);
    }

    private async Task BroadcastFppNowPlayingAsync(FppStatus fppStatus) {
        var played = int.TryParse(fppStatus.seconds_played, out var sp) ? sp : 0;
        var remaining = int.TryParse(fppStatus.seconds_remaining, out var sr) ? sr : 0;
        NowPlayingDto dto;
        if (!fppStatus.IsIdle && fppStatus.CurrentTrackName != null) {
            var trackName = Path.GetFileNameWithoutExtension(fppStatus.CurrentTrackName);
            dto = new NowPlayingDto(trackName, null, fppStatus.CurrentTrackName, null, true, _state.ToString(), played, remaining);
        } else {
            dto = new NowPlayingDto(null, null, null, null, false, _state.ToString(), 0, 0);
        }
        await hub.Clients.All.SendAsync("NowPlayingChanged", dto);
    }

    private async Task<Models.ShowConfig> GetConfigAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ShowConfigs.FindAsync(1) ?? new Models.ShowConfig();
    }
}
