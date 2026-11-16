using System.Globalization;

namespace ArctraWin.App.Observer;

public static class ObserverIni
{
    public static ObserverSettings Parse(string content, ObserverSettings displaySettings)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var inSection = false;
        using var reader = new StringReader(content.TrimStart('\uFEFF'));
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#') || text.StartsWith(';')) continue;
            if (text.StartsWith('[') && text.EndsWith(']')) { inSection = text[1..^1].Trim() == "observatory"; continue; }
            if (!inSection) continue;
            var separator = text.IndexOf('=');
            if (separator < 1) throw new InvalidDataException($"Expected key=value at line {lineNumber}.");
            var key = text[..separator].Trim();
            if (key is not ("name" or "lat" or "lon" or "height" or "min-elevation")) throw new InvalidDataException($"Unknown observatory key '{key}' at line {lineNumber}.");
            if (!values.TryAdd(key, text[(separator + 1)..].Trim())) throw new InvalidDataException($"Duplicate observatory key '{key}' at line {lineNumber}.");
        }
        string Required(string key) => values.TryGetValue(key, out var value) && value.Length > 0 ? value : throw new InvalidDataException($"Observer file requires '{key}'.");
        double Number(string key) => double.TryParse(Required(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value : throw new InvalidDataException($"Invalid number for '{key}'; use decimal degrees/metres and a decimal point.");
        return (displaySettings with { Name = Required("name"), LatitudeDeg = Number("lat"), LongitudeDeg = Number("lon"),
            EllipsoidalHeightM = Number("height"), MinElevationDeg = Number("min-elevation") }).Validated();
    }
    public static string Format(ObserverSettings settings)
    {
        var value = settings.Validated();
        return FormattableString.Invariant($"# Coordinates: decimal degrees; height: WGS 84 ellipsoidal metres.\n[observatory]\nname = {value.Name}\nlat = {value.LatitudeDeg:R}\nlon = {value.LongitudeDeg:R}\nheight = {value.EllipsoidalHeightM:R}\nmin-elevation = {value.MinElevationDeg:R}\n");
    }
}
