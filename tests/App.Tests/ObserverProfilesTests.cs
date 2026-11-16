using ArctraWin.App.Observer;

internal static class ObserverProfilesTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception ex) when (ex is InvalidDataException or IOException) { return; }
        throw new Exception("Invalid observer profile was accepted.");
    }
    public static void Run()
    {
        const string ini = "# Arctra format\n[unrelated]\nignore=yes\n[observatory]\nname = Ny-Aalesund SGSLR\nlat = 78.94300\nlon = 11.85580\nheight = 13.0\nmin-elevation = 12.5\n";
        var parsed = ObserverIni.Parse(ini, ObserverSettings.Default);
        Check(parsed.MinElevationDeg == 12.5 && parsed.LatitudeDeg == 78.943 && parsed.EllipsoidalHeightM == 13, "Arctra INI fields were not loaded.");
        Check(ObserverIni.Parse(ObserverIni.Format(parsed), parsed) == parsed, "INI round trip changed observer values.");
        Reject(() => ObserverIni.Parse(ini.Replace("min-elevation = 12.5", ""), parsed));
        Reject(() => ObserverIni.Parse(ini + "lat=1\n", parsed));
        Reject(() => ObserverIni.Parse(ini + "unknown=1\n", parsed));
        Reject(() => ObserverIni.Parse(ini.Replace("12.5", "91"), parsed));
        Reject(() => ObserverIni.Parse(ini.Replace("13.0", "13 metres"), parsed));
        Reject(() => ObserverIni.Parse(ini.Replace("78.94300", "78,943"), parsed));
        Reject(() => ObserverIni.Format(parsed with { Name = "name\nlat=1" }));
        var directory = Path.Combine(Path.GetTempPath(), "ArctraWin-Profiles-" + Guid.NewGuid());
        var settingsFile = Path.Combine(directory, "observer.json");
        var store = new ObserverProfileStore(Path.Combine(directory, "Observers"), Path.Combine(directory, "active.json"));
        try {
            store.Initialize(ObserverSettings.Default, true);
            Check(store.ReadSelection() == "previous-observer.ini", "Migration must preserve the current observer.");
            var profiles = store.Load(ObserverSettings.Default, out var errors);
            Check(profiles.Count == 2 && errors.Length == 0, "Seed and migrated profiles must both be available.");
            Check(profiles.Single(p => p.Filename == "nyal.ini").Settings.MinElevationDeg == 12.5, "Arctra example threshold missing.");
            Check(profiles.Single(p => p.Filename == "previous-observer.ini").Settings.MinElevationDeg == 0, "Existing zero limit must be preserved.");
            var name = store.UniqueFilename(parsed.Name);
            store.Save(name, parsed, false);
            Reject(() => store.Save(name, parsed with { MinElevationDeg = 30 }, false));
            Check(store.UniqueFilename(parsed.Name) != name, "Save as/import must not overwrite a profile.");
            store.Select(name);
            Check(store.ReadSelection() == name, "Active profile choice did not persist.");
            store.Save(name, parsed with { MinElevationDeg = 20 }, true);
            File.WriteAllText(Path.Combine(store.DirectoryPath, "bad.ini"), "[observatory]\nname=broken");
            profiles = store.Load(ObserverSettings.Default, out errors);
            Check(errors.Length == 1 && profiles.Single(p => p.Filename == name).Settings.MinElevationDeg == 20, "Reload must skip bad profiles and read edited thresholds.");
            Reject(() => store.Save("../bad.ini", parsed, true));
            store.Initialize(parsed, true);
            Check(store.ReadSelection() == name, "Startup must not reseed or reset existing profiles.");
            // Existing observer-v1.json files have neither of the new fields.
            File.WriteAllText(settingsFile, "{\"schemaVersion\":1,\"observer\":{\"name\":\"Existing\",\"latitudeDeg\":1,\"longitudeDeg\":2,\"ellipsoidalHeightM\":3,\"planeWidthKm\":12000,\"showPlane\":true}}");
            var legacy = ObserverSettingsStore.Load(settingsFile);
            Check(legacy.MinElevationDeg == 0 && legacy.ShowElevationCone, "Legacy settings must acquire sensible display defaults.");
        } finally {
            if (Directory.Exists(store.DirectoryPath)) {
                foreach (var file in Directory.EnumerateFiles(store.DirectoryPath)) File.Delete(file);
                Directory.Delete(store.DirectoryPath);
            }
            if (Directory.Exists(directory)) {
                foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
        Console.WriteLine("Observer INI, profile migration, selection, edit/import/save-as collision and reload checks passed.");
    }
}
