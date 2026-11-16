using System.Globalization;
using System.Text.Json;
using ArctraWin.App.Live;

namespace ArctraWin.App;

public sealed partial class MainWindow
{
    private void StartLiveTracking()
    {
        if (tracking is not null) return;
        tracking = new LiveTrackingService(Path.Combine(AppContext.BaseDirectory, "Backend", "arctrawin-live.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArctraWin", "cpf"));
        tracking.SetObserver(observer);
        tracking.FrameAvailable += frame => DispatcherQueue.TryEnqueue(() => OnLiveFrame(frame));
        tracking.Start();
    }

    private void OnLiveFrame(JsonElement frame)
    {
        if (closing || !globeReady || catalog is null) return;
        if (!frame.TryGetProperty("error", out _)) {
            if (!frame.TryGetProperty("observer", out var site)) {
                LiveBadge.Text = "LIVE · INCOMPATIBLE BACKEND";
                StatusText.Text = "Rebuild the backend: observer-limit contract is missing.";
                Send("clear-trajectory");
                return;
            }
            if (site.GetProperty("latitudeDeg").GetDouble() != observer.LatitudeDeg || site.GetProperty("longitudeDeg").GetDouble() != observer.LongitudeDeg ||
                site.GetProperty("ellipsoidalHeightM").GetDouble() != observer.EllipsoidalHeightM || site.GetProperty("minElevationDeg").GetDouble() != observer.MinElevationDeg) return;
        }
        var selected = catalog.Targets.Where(t => t.IsSelected).ToDictionary(t => t.Key, StringComparer.Ordinal);
        var states = frame.GetProperty("targets").EnumerateArray().ToArray();
        if (!frame.TryGetProperty("error", out _) && !selected.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(states.Select(t => t.GetProperty("key").GetString()!))) return;
        GlobeView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { command = "live-frame", frame }));
        if (frame.TryGetProperty("error", out var failure))
        {
            LiveBadge.Text = "LIVE · UNAVAILABLE";
            PreviewStatusText.Text = $"Live updates unavailable: {failure.GetString()}";
            foreach (var target in selected.Values) target.SetLiveState("Live updates unavailable", failure.GetString());
            PreviewButton.IsEnabled = ReferenceOrbitToggle.IsEnabled = TrajectoryToggle.IsEnabled = FrameTrajectoryButton.IsEnabled = false;
            StatusText.Text = "Live backend unavailable · positions hidden";
            return;
        }
        var ready = 0;
        var above = 0;
        var warnings = new List<string>();
        foreach (var state in states)
        {
            var key = state.GetProperty("key").GetString()!;
            if (!selected.TryGetValue(key, out var target)) continue;
            var status = state.GetProperty("state").GetString();
            var message = state.TryGetProperty("message", out var detail) ? detail.GetString() ?? "" : "";
            if (status == "ready")
            {
                ready++;
                var look = state.GetProperty("look");
                var visible = look.GetProperty("aboveThreshold").GetBoolean();
                if (visible) above++;
                var cpf = state.GetProperty("cpf");
                var label = $"{(visible ? "Above limit" : "Below limit")} · El {look.GetProperty("elevationDeg").GetDouble():F1}°";
                if (message.StartsWith("CPF refresh failed:", StringComparison.Ordinal)) { label += " · refresh warning"; warnings.Add($"{target.Name}: {message}"); }
                target.SetLiveState(label, $"{cpf.GetProperty("filename").GetString()}\nCPF valid: {cpf.GetProperty("usableStartUtc").GetString()} to {cpf.GetProperty("usableEndUtc").GetString()}\nAz {look.GetProperty("azimuthDeg").GetDouble():F3}° · El {look.GetProperty("elevationDeg").GetDouble():F3}° · Range {look.GetProperty("slantRangeM").GetDouble()/1000:F1} km\nRetrieved {cpf.GetProperty("retrievedAtUtc").GetString()}\n{message}");
            }
            else
            {
                target.SetLiveState(status switch { "expired" => "CPF expired · position hidden", "outside-coverage" => "Outside CPF coverage", "loading" => "Loading current CPF…", _ => "CPF unavailable" }, message);
                warnings.Add($"{target.Name}: {message}");
            }
        }
        var epoch = DateTimeOffset.Parse(frame.GetProperty("epochUtc").GetString()!, CultureInfo.InvariantCulture);
        LiveBadge.Text = $"LIVE CPF · {ready}/{selected.Count}";
        PreviewStatusText.Text = $"{epoch:HH:mm:ss} UTC · {ready}/{selected.Count} valid CPF · {above} above {observer.MinElevationDeg:0.##}°";
        if (warnings.Count > 0) PreviewStatusText.Text += "\n" + string.Join("\n", warnings.Take(2));
        StatusText.Text = $"Live CPF predictions · {epoch:yyyy-MM-dd HH:mm:ss} UTC · {observer.Name} · minimum elevation {observer.MinElevationDeg:0.##}°";
        PreviewButton.IsEnabled = ReferenceOrbitToggle.IsEnabled = TrajectoryToggle.IsEnabled = FrameTrajectoryButton.IsEnabled = ready > 0;
    }
}
