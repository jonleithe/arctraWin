using System.Text.Json;

namespace ArctraWin.App.Observer;

public sealed record ObserverSettings(string Name, double LatitudeDeg, double LongitudeDeg,
    double EllipsoidalHeightM, double PlaneWidthKm, bool ShowPlane, double MinElevationDeg = 0, bool ShowElevationCone = true)
{
    public static ObserverSettings Default => new("Ny-Ålesund", 78.923, 11.923, 0, 12000, true);

    public ObserverSettings Validated()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 80 || Name.Any(char.IsControl))
            throw new InvalidDataException("Enter an observer name of 1–80 characters.");
        if (!double.IsFinite(LatitudeDeg) || LatitudeDeg < -90 || LatitudeDeg > 90)
            throw new InvalidDataException("Latitude must be between −90° and 90° (north positive).");
        if (!double.IsFinite(LongitudeDeg) || LongitudeDeg < -180 || LongitudeDeg > 180)
            throw new InvalidDataException("Longitude must be between −180° and 180° (east positive).");
        if (!double.IsFinite(EllipsoidalHeightM) || EllipsoidalHeightM < -1000 || EllipsoidalHeightM > 100000)
            throw new InvalidDataException("Ellipsoidal height must be between −1000 and 100000 metres.");
        if (!double.IsFinite(PlaneWidthKm) || PlaneWidthKm < 500 || PlaneWidthKm > 40000)
            throw new InvalidDataException("Plane width must be between 500 and 40000 kilometres.");
        if (!double.IsFinite(MinElevationDeg) || MinElevationDeg < -90 || MinElevationDeg > 90)
            throw new InvalidDataException("Minimum elevation must be between −90° and 90°.");
        return this with { Name = Name.Trim() };
    }
}

public static class ObserverSettingsStore
{
    private sealed record Document(int SchemaVersion, ObserverSettings Observer);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static ObserverSettings Load(string path)
    {
        if (!File.Exists(path)) return ObserverSettings.Default;
        var text = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<Document>(text, JsonOptions);
        if (document is null || document.SchemaVersion != 1 || document.Observer is null)
            throw new InvalidDataException("Unsupported observer settings format.");
        using var parsed = JsonDocument.Parse(text);
        var settings = document.Observer;
        if (!parsed.RootElement.GetProperty("observer").TryGetProperty("showElevationCone", out _)) settings = settings with { ShowElevationCone = true };
        return settings.Validated();
    }
    public static void Save(string path, ObserverSettings observer)
    {
        var document = new Document(1, observer.Validated());
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(document, JsonOptions));
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
