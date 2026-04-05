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
            logger.LogDebug("FPP idle but '{Title}' only started {Seconds:F0}s ago — likely still loading, skipping played check", playingItem.Song.Title, playingSecondsElapsed);
        if (playingItem != null && fppStatus.IsIdle && playingSecondsElapsed >= MinPlayedSeconds) {
            logger.LogInformation("Queue song '{Title}' finished (played {Seconds:F0}s)", playingItem.Song.Title, playingSecondsElapsed);
            await queueService.MarkPlayedAsync(playingItem.Id);
            _activeQueueItemId = null;
            await BroadcastQueueAsync();
            await mqttService.PublishAsync("nowplaying", new { title = (string?)null, playing = false });
            playingItem = null;
        }

        var config = await GetConfigAsync();

        if (!config.IsSeasonActive) {
            // Season is off — stop anything playing and go idle
            if (fppStatus.IsPlayingPlaylist && playingItem == null) {
                logger.LogInformation("Season inactive — stopping default playlist");
                await fppService.StopPlaylistAsync();
                RecordCommand();
            }
            SetState(QueueManagerState.Idle, SyncStatus.InSync);
            return;
        }

        var isOpen = ScheduleHelper.IsOpen(config.ShowScheduleJson);

        if (!isOpen) {
            // Outside show hours — stop the default playlist if it's looping, do nothing else.
            if (fppStatus.IsPlayingPlaylist && playingItem == null) {
                logger.LogInformation("Outside show hours — stopping default playlist");
                await fppService.StopPlaylistAsync();
                RecordCommand();
            }
            SetState(QueueManagerState.Idle, SyncStatus.InSync);
            return;
        }

        if (nextPending != null) {
            // FPP is playing something we didn't queue (default playlist or standalone sequence) → stop it.
            // Never stop xlq_current — that's our own temp playlist started by this service.
            var fppPlayingOurPlaylist = fppStatus.current_playlist?.playlist == "xlq_current";
            var fppPlayingUnqueued = (fppStatus.IsPlayingPlaylist || fppStatus.IsPlayingSequence)
                && playingItem == null && !fppPlayingOurPlaylist;
            if (fppPlayingUnqueued && config.InterruptForUserSongs) {
                logger.LogInformation("Stopping FPP playback to play queue song '{Title}'", nextPending.Song.Title);
                await fppService.StopPlaylistAsync();
                RecordCommand();
                return;
            }

            if (fppStatus.IsIdle) {
                logger.LogInformation("Starting queue song '{Title}'", nextPending.Song.Title);
                await fppService.StartSequenceAsync(nextPending.Song.Filename, nextPending.Song.MediaFilename);
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
            if (fppStatus.IsIdle && config.AutoPlayDefault && !string.IsNullOrEmpty(config.DefaultPlaylistName)) {
                logger.LogInformation("Starting default playlist '{Name}'", config.DefaultPlaylistName);
                await fppService.StartPlaylistAsync(config.DefaultPlaylistName);
                RecordCommand();
                SetState(QueueManagerState.PlayingDefault, SyncStatus.InSync);
            } else if (fppStatus.IsPlayingPlaylist)
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
