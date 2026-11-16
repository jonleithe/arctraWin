using System.Text.Json;
using ArctraWin.App.Catalog;
using ArctraWin.App.Live;
using ArctraWin.App.Observer;

internal static class LiveIntegration
{
    public static async Task RunAsync(string executable, string cacheDirectory)
    {
        var catalog = SatelliteCatalog.Load(Path.Combine(AppContext.BaseDirectory, "catalog.csv"));
        var targets = catalog.Targets.Where(t => new[] { "lageos1", "lageos2", "lares", "lares2" }.Contains(t.Key)).ToArray();
        using var service = new LiveTrackingService(Path.GetFullPath(executable), Path.GetFullPath(cacheDirectory));
        service.SetSelection(targets);
        service.SetObserver(ObserverSettings.Default);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var epochs = new HashSet<string>();
        JsonElement? previousPosition = null;
        var changedPosition = false;
        var phase = 0;
        var previousElevation = 0.0;
        var lastStatus = "";
        var received = new List<JsonElement>();
        service.FrameAvailable += frame => {
            try {
                if (frame.TryGetProperty("error", out var error)) { Console.WriteLine(error.GetString()); return; }
                var states = frame.GetProperty("targets").EnumerateArray().ToArray();
                var ready = states.Where(s => s.GetProperty("state").GetString() == "ready").ToArray();
                var status = string.Join(" | ", states.Select(s => s.GetProperty("key").GetString() + ":" + s.GetProperty("state").GetString() + ":" + (s.TryGetProperty("message", out var m) ? m.GetString() : "")));
                if (status != lastStatus) { Console.WriteLine(status); lastStatus = status; }
                var epoch = frame.GetProperty("epochUtc").GetString()!;
                if (phase == 0 && ready.Length == 4) {
                    var position = ready[0].GetProperty("positionEcefM");
                    if (previousPosition.HasValue && previousPosition.Value.GetRawText() != position.GetRawText()) changedPosition = true;
                    previousPosition = position.Clone();
                    epochs.Add(epoch);
                    received.Add(frame.Clone());
                    Console.WriteLine($"{epoch}: {ready.Length} current CPF states");
                    if (epochs.Count >= 3 && changedPosition) {
                        phase = 1;
                        service.SetSelection(targets.Take(2));
                    }
                } else if (phase == 1 && states.Length == 2 && ready.Length == 2) {
                    phase = 2;
                    previousElevation = ready[0].GetProperty("look").GetProperty("elevationDeg").GetDouble();
                    service.SetObserver(new ObserverSettings("Perth test", -31.95, 115.86, 35, 12000, true));
                } else if (phase == 2 && states.Length == 2 && ready.Length == 2 && Math.Abs(ready[0].GetProperty("look").GetProperty("elevationDeg").GetDouble() - previousElevation) > 1) {
                    phase = 3;
                    service.SetObserver(new ObserverSettings("Perth test", -31.95, 115.86, 35, 12000, true, 90));
                } else if (phase == 3 && states.Length == 2 && ready.Length == 2 && frame.GetProperty("observer").GetProperty("minElevationDeg").GetDouble() == 90 && ready.All(s => !s.GetProperty("look").GetProperty("aboveThreshold").GetBoolean())) {
                    phase = 4;
                    service.SetSelection([]);
                } else if (phase == 4 && states.Length == 0) completion.TrySetResult();
            } catch (Exception ex) { completion.TrySetException(ex); }
        };
        service.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(150));
        await File.WriteAllTextAsync(Path.Combine(Path.GetFullPath(cacheDirectory), "live-proof.json"), JsonSerializer.Serialize(received));
        Console.WriteLine("Live integration passed: real EDC acquisition, advancing one-second positions, observer/threshold changes, partial deselection, and empty selection.");
    }
}
