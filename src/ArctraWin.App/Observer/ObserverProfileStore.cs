using System.Text.Json;

namespace ArctraWin.App.Observer;

public sealed record ObserverProfile(string Filename, ObserverSettings Settings)
{
    public string DisplayName => $"{Settings.Name} · ≥ {Settings.MinElevationDeg:0.##}° ({Filename})";
}

public sealed class ObserverProfileStore(string directory, string selectionPath)
{
    private sealed record Selection(int SchemaVersion, string ActiveFile);
    public string DirectoryPath => directory;
    public void Initialize(ObserverSettings previous, bool migratePrevious)
    {
        Directory.CreateDirectory(directory);
        if (!Directory.EnumerateFiles(directory, "*.ini").Any())
        {
            var nyal = new ObserverSettings("Ny-Aalesund SGSLR", 78.943, 11.8558, 13, previous.PlaneWidthKm, previous.ShowPlane, 12.5, previous.ShowElevationCone);
            Save("nyal.ini", nyal, overwrite: false);
        }
        if (File.Exists(selectionPath)) return;
        var existing = Load(previous, out _);
        if (migratePrevious) {
            var match = existing.FirstOrDefault(profile => profile.Settings == previous);
            var initial = match?.Filename ?? UniqueFilename("previous-observer");
            if (match is null) Save(initial, previous, overwrite: false);
            Select(initial);
        } else if (existing.Count > 0) Select(existing[0].Filename);
    }
    public IReadOnlyList<ObserverProfile> Load(ObserverSettings displaySettings, out string[] errors)
    {
        var profiles = new List<ObserverProfile>();
        var failures = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.ini").Order(StringComparer.OrdinalIgnoreCase))
        {
            try { profiles.Add(new ObserverProfile(Path.GetFileName(path), ObserverIni.Parse(File.ReadAllText(path), displaySettings))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { failures.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
        }
        errors = failures.ToArray();
        return profiles;
    }
    public string? ReadSelection()
    {
        if (!File.Exists(selectionPath)) return null;
        var value = JsonSerializer.Deserialize<Selection>(File.ReadAllText(selectionPath));
        if (value is null || value.SchemaVersion != 1 || !IsFilename(value.ActiveFile)) throw new InvalidDataException("Invalid observer profile selection.");
        return value.ActiveFile;
    }
    public void Select(string filename)
    {
        if (!IsFilename(filename) || !File.Exists(Path.Combine(directory, filename))) throw new InvalidDataException("Observer profile not found.");
        AtomicWrite(selectionPath, JsonSerializer.Serialize(new Selection(1, filename)), true);
    }
    public void Save(string filename, ObserverSettings settings, bool overwrite)
    {
        if (!IsFilename(filename)) throw new InvalidDataException("Invalid observer filename.");
        AtomicWrite(Path.Combine(directory, filename), ObserverIni.Format(settings), overwrite);
    }
    public string UniqueFilename(string name)
    {
        var stem = new string(name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (stem.Length == 0) stem = "observer";
        stem = stem[..Math.Min(stem.Length, 50)];
        var candidate = stem + ".ini";
        for (var index = 2; File.Exists(Path.Combine(directory, candidate)); index++) candidate = $"{stem}-{index}.ini";
        return candidate;
    }
    private static bool IsFilename(string? name) => !string.IsNullOrWhiteSpace(name) && Path.GetFileName(name) == name &&
        name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    private static void AtomicWrite(string path, string text, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false)); File.Move(temporary, path, overwrite); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
