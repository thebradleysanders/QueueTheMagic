using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using XlightsQueue.Data;
using XlightsQueue.DTOs;

namespace XlightsQueue.Services;

public class FppScheduleEntry {
    public int enabled { get; set; }
    public string playlist { get; set; } = string.Empty;
    public int day { get; set; }
    public string startTime { get; set; } = string.Empty;
    public string endTime { get; set; } = string.Empty;
    public int startTimeOffset { get; set; }
    public int endTimeOffset { get; set; }
    public string startDate { get; set; } = string.Empty;
    public string endDate { get; set; } = string.Empty;
}

public class FppCurrentPlaylist {
    public string playlist { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
}

public class FppStatus {
    public string fppd { get; set; } = string.Empty;
    public string status_name { get; set; } = string.Empty;
    public int status { get; set; }
    public string current_sequence { get; set; } = string.Empty;
    public string current_song { get; set; } = string.Empty;
    public FppCurrentPlaylist current_playlist { get; set; } = new();
    public string seconds_remaining { get; set; } = "0";
    public string seconds_played { get; set; } = "0";

    [JsonIgnore]
    public bool IsIdle => status_name == "idle" || status == 0;
    [JsonIgnore]
    public bool IsPlayingSequence => !string.IsNullOrEmpty(current_sequence);
    [JsonIgnore]
    public bool IsPlayingPlaylist => !string.IsNullOrEmpty(current_playlist?.playlist);
    [JsonIgnore]
    public string? CurrentTrackName => !string.IsNullOrEmpty(current_song) ? current_song
        : !string.IsNullOrEmpty(current_sequence) ? current_sequence
        : null;
}

public class FppService(IDbContextFactory<AppDbContext> dbFactory, ILogger<FppService> logger) {
    private readonly ConcurrentQueue<FppCommandLogEntry> _commandLog = new();
    private const int MaxLogEntries = 30;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task<string> GetFppAddressAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var config = await db.ShowConfigs.FindAsync(1);
        return config?.FppAddress ?? "192.168.1.100";
    }

    private async Task<HttpResponseMessage?> CallAsync(string path) {
        var address = await GetFppAddressAsync();
        var url = $"http://{address}{path}";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int statusCode = 0;
        try {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(url);
            statusCode = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode) {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogWarning("FPP non-2xx: {StatusCode} {Url} — {Body}", statusCode, url, body);
            }
            return response;
        } catch (Exception ex) {
            logger.LogWarning("FPP call failed: {Url} — {Message}", url, ex.Message);
            return null;
        } finally {
            sw.Stop();
            EnqueueLog(new FppCommandLogEntry(DateTime.UtcNow, url, statusCode, sw.ElapsedMilliseconds));
        }
    }

    private async Task<HttpResponseMessage?> PostJsonAsync(string path, string jsonBody) {
        var address = await GetFppAddressAsync();
        var url = $"http://{address}{path}";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int statusCode = 0;
        try {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            statusCode = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode) {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogWarning("FPP POST non-2xx: {StatusCode} {Url} — {Body}", statusCode, url, body);
            }
            return response;
        } catch (Exception ex) {
            logger.LogWarning("FPP POST failed: {Url} — {Message}", url, ex.Message);
            return null;
        } finally {
            sw.Stop();
            EnqueueLog(new FppCommandLogEntry(DateTime.UtcNow, $"POST {url}", statusCode, sw.ElapsedMilliseconds));
        }
    }

    private async Task<HttpResponseMessage?> PostCommandAsync(string command, object[] args) {
        var address = await GetFppAddressAsync();
        var url = $"http://{address}/api/command";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int statusCode = 0;
        try {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var body = JsonSerializer.Serialize(new { command, args });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            statusCode = (int)response.StatusCode;
            return response;
        } catch (Exception ex) {
            logger.LogWarning("FPP command failed: {Command} — {Message}", command, ex.Message);
            return null;
        } finally {
            sw.Stop();
            EnqueueLog(new FppCommandLogEntry(DateTime.UtcNow, $"{url} [{command}]", statusCode, sw.ElapsedMilliseconds));
        }
    }

    private void EnqueueLog(FppCommandLogEntry entry) {
        _commandLog.Enqueue(entry);
        while (_commandLog.Count > MaxLogEntries)
            _commandLog.TryDequeue(out _);
    }

    public List<FppCommandLogEntry> GetCommandLog() => _commandLog.ToList();

    public async Task<FppStatus?> GetStatusAsync() {
        var response = await CallAsync("/api/fppd/status");
        if (response?.IsSuccessStatusCode != true) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<FppStatus>(json, _jsonOptions);
    }

    // Creates a temporary single-song playlist and starts it.
    // FPP's /api/sequence/{name}/start is for overlay sequences only — main playback requires a playlist.
    public async Task<bool> StartSequenceAsync(string sequenceName, string mediaName = "") {
        const string TempPlaylist = "xlq_current";

        // Ensure .fseq extension — /api/sequence list omits it but FPP playlist entries require it
        if (!sequenceName.EndsWith(".fseq", StringComparison.OrdinalIgnoreCase))
            sequenceName += ".fseq";
        var entryType = string.IsNullOrEmpty(mediaName) ? "sequence" : "both";
        var entry = new {
            type = entryType,
            enabled = 1,
            playOnce = 0,
            sequenceName,
            mediaName,
            duration = 0
        };
        var payload = JsonSerializer.Serialize(new {
            name = TempPlaylist,
            repeat = 0,
            loopCount = 0,
            random = 0,
            leadIn = Array.Empty<object>(),
            mainPlaylist = new[] { entry },
            leadOut = Array.Empty<object>()
        });

        var created = await PostJsonAsync($"/api/playlist/{TempPlaylist}", payload);
        if (created?.IsSuccessStatusCode != true) {
            logger.LogWarning("Failed to create temp playlist for '{Sequence}'", sequenceName);
            return false;
        }

        var started = await CallAsync($"/api/playlist/{TempPlaylist}/start");
        return started?.IsSuccessStatusCode == true;
    }

    // GET /api/playlist/{name}/start
    public async Task<bool> StartPlaylistAsync(string name) {
        var response = await CallAsync($"/api/playlist/{Uri.EscapeDataString(name)}/start");
        return response?.IsSuccessStatusCode == true;
    }

    // GET /api/playlists/stop
    public async Task<bool> StopPlaylistAsync() {
        var response = await CallAsync("/api/playlists/stop");
        return response?.IsSuccessStatusCode == true;
    }

    // GET /api/playlist/{name} → parse entries from mainPlaylist section
    public async Task<List<(string SequenceName, string MediaName, double Duration)>> GetPlaylistSequencesAsync(string name) {
        var response = await CallAsync($"/api/playlist/{Uri.EscapeDataString(name)}");
        if (response?.IsSuccessStatusCode != true) return [];
        var json = await response.Content.ReadAsStringAsync();

        var result = new List<(string, string, double)>();
        try {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            foreach (var sectionKey in new[] { "mainPlaylist", "main_playlist" }) {
                if (!root.TryGetProperty(sectionKey, out var section)) continue;
                if (section.ValueKind != JsonValueKind.Array) continue;

                foreach (var entry in section.EnumerateArray()) {
                    var seqName = string.Empty;
                    foreach (var key in new[] { "sequenceName", "sequence_name" })
                        if (entry.TryGetProperty(key, out var p)) { seqName = p.GetString() ?? ""; break; }

                    if (string.IsNullOrEmpty(seqName)) continue;

                    var mediaName = string.Empty;
                    foreach (var key in new[] { "mediaName", "media_name" })
                        if (entry.TryGetProperty(key, out var p)) { mediaName = p.GetString() ?? ""; break; }

                    double dur = 0;
                    if (entry.TryGetProperty("duration", out var d)) d.TryGetDouble(out dur);

                    result.Add((seqName, mediaName, dur));
                }
                break;
            }
        } catch (Exception ex) {
            logger.LogWarning("Failed to parse FPP playlist detail for '{Name}': {Message}", name, ex.Message);
        }

        return result;
    }

    // GET /api/schedule → array of schedule entries
    public async Task<List<FppScheduleEntry>> GetScheduleAsync() {
        var response = await CallAsync("/api/schedule");
        if (response?.IsSuccessStatusCode != true) return [];
        var json = await response.Content.ReadAsStringAsync();
        try { return JsonSerializer.Deserialize<List<FppScheduleEntry>>(json, _jsonOptions) ?? []; } catch { return []; }
    }

    // Maps FPP day integer to the weekday names it covers (0=Sunday..6=Saturday)
    public static IEnumerable<string> FppDayToDayNames(int day) {
        string[] all = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
        return day switch {
            >= 0 and <= 6 => [all[day]],
            7 => all,                                                              // Every Day
            8 => ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],        // Weekdays
            9 => ["Saturday", "Sunday"],                                           // Weekends
            10 => ["Monday", "Wednesday", "Friday"],                               // Mon/Wed/Fri
            11 => ["Tuesday", "Thursday"],                                          // Tue/Thu
            12 => ["Monday", "Tuesday", "Wednesday", "Thursday"],                  // Mon–Thu
            13 => ["Friday", "Saturday"],                                           // Fri–Sat
            _ => []
        };
    }

    // GET /api/playlists → plain JSON array
    public async Task<List<string>> GetPlaylistsAsync() {
        var response = await CallAsync("/api/playlists");
        if (response?.IsSuccessStatusCode != true) return [];
        var json = await response.Content.ReadAsStringAsync();
        try { return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? []; } catch { return []; }
    }

    // GET /api/sequence → plain JSON array of names
    public async Task<List<string>> GetSequencesAsync() {
        var response = await CallAsync("/api/sequence");
        if (response?.IsSuccessStatusCode != true) return [];
        var json = await response.Content.ReadAsStringAsync();
        try { return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? []; } catch { return []; }
    }
}
