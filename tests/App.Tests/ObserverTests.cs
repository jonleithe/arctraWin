using ArctraWin.App.Observer;

internal static class ObserverTests
{
    public static void Run()
    {
        var observer = new ObserverSettings(" Perth ", -31.95, 115.86, 35, 16000, true).Validated();
        if (observer.Name != "Perth") throw new Exception("Observer names should be trimmed.");
        foreach (var invalid in new[] {
            observer with { LatitudeDeg = 91 }, observer with { LongitudeDeg = -181 },
            observer with { LatitudeDeg = double.NaN }, observer with { EllipsoidalHeightM = double.PositiveInfinity },
            observer with { Name = " " }, observer with { PlaneWidthKm = 0 }, observer with { EllipsoidalHeightM = -2000 },
        })
        {
            var rejected = false;
            try { invalid.Validated(); } catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid observer settings were accepted.");
        }
        (observer with { LatitudeDeg = 90, LongitudeDeg = 180 }).Validated();
        (observer with { LatitudeDeg = -90, LongitudeDeg = -180 }).Validated();
        var directory = Path.Combine(Path.GetTempPath(), "ArctraWin-ObserverTests-" + Guid.NewGuid());
        var path = Path.Combine(directory, "observer.json");
        try
        {
            if (ObserverSettingsStore.Load(path) != ObserverSettings.Default) throw new Exception("Missing settings should use default observer.");
            ObserverSettingsStore.Save(path, observer);
            if (ObserverSettingsStore.Load(path) != observer) throw new Exception("Observer coordinates did not survive restart.");
            var hidden = observer with { ShowPlane = false };
            ObserverSettingsStore.Save(path, hidden);
            if (ObserverSettingsStore.Load(path) != hidden) throw new Exception("Plane visibility did not survive restart.");
            File.WriteAllText(path, "{\"schemaVersion\":99,\"observer\":null}");
            var rejected = false;
            try { ObserverSettingsStore.Load(path); } catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new Exception("Unknown observer formats must be rejected.");
        }
        finally
        {
            if (Directory.Exists(directory)) {
                foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
        Console.WriteLine("Observer validation and persistence checks passed.");
    }
}
