using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ArctraWin.App.Catalog;
using ArctraWin.App.Observer;

namespace ArctraWin.App.Live;

public sealed class LiveTrackingService : IDisposable
{
    private sealed record Target(string Key, string Name, int NoradId, int IlrsId);
    private sealed record CacheMetadata(string SourceUrl, string RetrievedAtUtc);
    private sealed record State(string? Path = null, CacheMetadata? Metadata = null,
        string Message = "Loading current CPF", string Status = "loading", DateTimeOffset? CoverageEnd = null, DateTimeOffset? NextCheck = null);
    private readonly object sync = new();
    private readonly Dictionary<string, State> states = new(StringComparer.Ordinal);
    private Target[] selected = [];
    private ObserverSettings observer = ObserverSettings.Default;
    private long revision;
    private readonly BackendConnection backend;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly CancellationTokenSource shutdown = new();
    private readonly string cacheDirectory;
    private string? listing;
    private DateTimeOffset listingAt;
    private bool started;
    private string geometrySignature = "";
    public event Action<JsonElement>? FrameAvailable;

    public LiveTrackingService(string executable, string cacheDirectory)
    {
        backend = new BackendConnection(executable);
        this.cacheDirectory = cacheDirectory;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ArctraWin/0.1");
    }
    public void SetSelection(IEnumerable<SatelliteTarget> targets)
    {
        lock (sync)
        {
            selected = targets.Where(t => t.IsSelectable).Select(t => new Target(t.Key, t.Name,
                int.Parse(t.NoradId!, CultureInfo.InvariantCulture), int.Parse(t.IlrsId, CultureInfo.InvariantCulture))).ToArray();
            foreach (var target in selected) states.TryAdd(target.Key, new State());
            revision++;
        }
    }
    public void SetObserver(ObserverSettings value) { lock (sync) { observer = value.Validated(); revision++; } }
    public void Start()
    {
        if (started) return;
        started = true;
        _ = TickLoopAsync(shutdown.Token);
        _ = RefreshLoopAsync(shutdown.Token);
    }
    private static string Utc(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    private object RequestTarget(Target target, State state) => new {
        key = target.Key, name = target.Name, noradId = target.NoradId, ilrsId = target.IlrsId,
        path = state.Path ?? "", sourceUrl = state.Metadata?.SourceUrl ?? "", retrievedAtUtc = state.Metadata?.RetrievedAtUtc ?? "",
        state = state.Status, message = state.Message,
    };
    private async Task TickLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                Target[] targets; ObserverSettings site; long generation; State[] values;
                lock (sync) { targets = selected; site = observer; generation = revision; values = targets.Select(t => states[t.Key]).ToArray(); }
                var now = DateTimeOffset.UtcNow;
                var signature = string.Join('|', values.Select((s, i) => targets[i].Key + ":" + s.Path)) + ":" + now.ToUnixTimeSeconds() / 60;
                try
                {
                    var frame = await backend.SendAsync(new {
                        schemaVersion = 1, operation = "evaluate", epochUtc = Utc(now),
                        observer = new { latitudeDeg = site.LatitudeDeg, longitudeDeg = site.LongitudeDeg, ellipsoidalHeightM = site.EllipsoidalHeightM, minElevationDeg = site.MinElevationDeg },
                        targets = targets.Select((t, i) => RequestTarget(t, values[i])).ToArray(), includeGeometry = signature != geometrySignature,
                    }, token).ConfigureAwait(false);
                    lock (sync) {
                        if (generation != revision) continue; // never paint a deselected target or old observer result
                        geometrySignature = signature;
                        FrameAvailable?.Invoke(frame);
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    geometrySignature = "";
                    using var error = JsonDocument.Parse(JsonSerializer.Serialize(new { schemaVersion = 1, epochUtc = Utc(now), error = ex.Message, targets = Array.Empty<object>() }));
                    FrameAvailable?.Invoke(error.RootElement.Clone());
                }
            } while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) when (token.IsCancellationRequested) { }
    }
    private async Task RefreshLoopAsync(CancellationToken token)
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            while (!token.IsCancellationRequested)
            {
                Target[] targets;
                lock (sync) targets = selected;
                foreach (var target in targets)
                {
                    State current;
                    lock (sync) {
                        if (!selected.Any(t => t.Key == target.Key)) continue;
                        current = states[target.Key];
                    }
                    if (current.NextCheck > DateTimeOffset.UtcNow) continue;
                    try { await AcquireAsync(target, current, token).ConfigureAwait(false); }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        lock (sync) {
                            var retained = states[target.Key];
                            states[target.Key] = retained with { Status = retained.Path is null ? "error" : "ready",
                                Message = $"CPF refresh failed: {ex.Message}", NextCheck = DateTimeOffset.UtcNow.AddMinutes(5) };
                        }
                    }
                }
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) {
            lock (sync) foreach (var key in states.Keys.ToArray()) states[key] = states[key] with { Status = "error", Message = ex.Message };
        }
    }
    private async Task<JsonElement> ValidateAsync(Target target, string path, CacheMetadata metadata, CancellationToken token) =>
        await backend.SendAsync(new { schemaVersion = 1, operation = "validate", epochUtc = Utc(DateTimeOffset.UtcNow),
            target = RequestTarget(target, new State(path, metadata)) }, token).ConfigureAwait(false);

    private async Task AcquireAsync(Target target, State current, CancellationToken token)
    {
        if (current.Path is null)
        {
            foreach (var path in Directory.EnumerateFiles(cacheDirectory, target.Key + "_cpf_*")
                .Where(p => !p.EndsWith(".json", StringComparison.Ordinal) && !p.EndsWith(".part", StringComparison.Ordinal))
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
            {
                try {
                    var metadata = JsonSerializer.Deserialize<CacheMetadata>(await File.ReadAllTextAsync(path + ".json", token).ConfigureAwait(false));
                    if (!ValidMetadata(metadata, Path.GetFileName(path))) continue;
                    var response = await ValidateAsync(target, path, metadata!, token).ConfigureAwait(false);
                    current = Ready(path, metadata!, response, DateTimeOffset.UtcNow);
                    lock (sync) states[target.Key] = current with { Message = "Using validated cache; checking EDC" };
                    break;
                } catch (Exception) when (!token.IsCancellationRequested) { /* try the next cached issue */ }
            }
        }
        var now = DateTimeOffset.UtcNow;
        if (listing is null || now - listingAt > TimeSpan.FromMinutes(1)) {
            listing = await http.GetStringAsync(CpfRefreshPolicy.DirectoryUrl, token).ConfigureAwait(false);
            listingAt = now;
        }
        var filename = CpfRefreshPolicy.SelectFilename(target.Key, listing) ?? throw new InvalidDataException("EDC has no current CPF for this target.");
        var destination = Path.Combine(cacheDirectory, filename);
        var sourceUrl = CpfRefreshPolicy.DirectoryUrl + filename;
        CacheMetadata? provenance = null;
        if (File.Exists(destination + ".json")) {
            try { provenance = JsonSerializer.Deserialize<CacheMetadata>(await File.ReadAllTextAsync(destination + ".json", token).ConfigureAwait(false)); }
            catch (JsonException) { }
        }
        if (!ValidMetadata(provenance, filename)) provenance = null;
        if (!File.Exists(destination) || provenance is null)
        {
            var bytes = await http.GetByteArrayAsync(sourceUrl, token).ConfigureAwait(false);
            if (bytes.Length > 16 * 1024 * 1024) throw new InvalidDataException("CPF download is unexpectedly large.");
            provenance = new CacheMetadata(sourceUrl, Utc(DateTimeOffset.UtcNow));
            var temporary = destination + ".part";
            try {
                await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
                await ValidateAsync(target, temporary, provenance, token).ConfigureAwait(false);
                File.Move(temporary, destination, overwrite: true);
                await File.WriteAllTextAsync(destination + ".json", JsonSerializer.Serialize(provenance), token).ConfigureAwait(false);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        var validated = await ValidateAsync(target, destination, provenance, token).ConfigureAwait(false);
        lock (sync) states[target.Key] = Ready(destination, provenance, validated, DateTimeOffset.UtcNow);
    }
    private static bool ValidMetadata(CacheMetadata? metadata, string filename) => metadata is not null &&
        metadata.SourceUrl == CpfRefreshPolicy.DirectoryUrl + filename && DateTimeOffset.TryParse(metadata.RetrievedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);
    private static State Ready(string path, CacheMetadata provenance, JsonElement response, DateTimeOffset now)
    {
        var end = DateTimeOffset.Parse(response.GetProperty("cpf").GetProperty("usableEndUtc").GetString()!, CultureInfo.InvariantCulture);
        return new State(path, provenance, "", "ready", end, CpfRefreshPolicy.NextCheck(now, end, true));
    }
    public void Dispose() { shutdown.Cancel(); backend.Dispose(); http.Dispose(); }
}
