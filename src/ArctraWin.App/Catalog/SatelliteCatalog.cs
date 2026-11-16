using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace ArctraWin.App.Catalog;

public sealed class SatelliteTarget : INotifyPropertyChanged
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string IlrsId { get; init; }
    public required string? NoradId { get; init; }
    public bool IsSelectable => NoradId is not null;
    private string? liveStatus;
    private string? liveDetails;
    public string Details => liveDetails ?? Availability;
    public void SetLiveState(string? status, string? details)
    {
        if (liveStatus == status && liveDetails == details) return;
        liveStatus = status;
        liveDetails = details;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Availability)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Details)));
    }
    public string Identifiers => $"NORAD {NoradId ?? "—"}   ·   ILRS {IlrsId}";
    private bool hasRecordedPreview;
    public bool HasRecordedPreview
    {
        get => hasRecordedPreview;
        set
        {
            if (hasRecordedPreview == value) return;
            hasRecordedPreview = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Availability)));
        }
    }
    public string Availability => liveStatus ?? (!IsSelectable ? "Lunar target · not supported" :
        HasRecordedPreview ? "Recorded CPF preview available" : "CPF candidate · prediction not loaded");
    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            var next = value && IsSelectable;
            if (isSelected == next) return;
            isSelected = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class SatelliteCatalog
{
    public const string SnapshotLabel = "EDC catalog · snapshot 13 Nov 2026";
    public IReadOnlyList<SatelliteTarget> Targets { get; }
    private SatelliteCatalog(List<SatelliteTarget> targets) => Targets = targets.AsReadOnly();

    public static SatelliteCatalog Load(string path) => Parse(File.ReadAllText(path));
    public static SatelliteCatalog Parse(string csv)
    {
        var targets = new List<SatelliteTarget>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        using var reader = new StringReader(csv);
        if (reader.ReadLine()?.TrimStart('\uFEFF') != "cpf_key,name,ilrs_id,norad_id")
            throw new InvalidDataException("The satellite catalog header is invalid.");
        string? line;
        var lineNumber = 1;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var fields = line.Split(',');
            if (fields.Length != 4 || fields[0].Length == 0 || string.IsNullOrWhiteSpace(fields[1]) ||
                fields[0].Any(c => !char.IsAsciiLetterOrDigit(c) || char.IsUpper(c)) ||
                fields[2].Length != 7 || !IsDecimal(fields[2]) ||
                (fields[3].Length > 0 && !IsDecimal(fields[3])) || !keys.Add(fields[0]))
                throw new InvalidDataException($"Invalid satellite catalog row {lineNumber}.");
            targets.Add(new SatelliteTarget { Key = fields[0], Name = fields[1], IlrsId = fields[2],
                NoradId = fields[3].Length == 0 ? null : fields[3] });
        }
        if (targets.Count == 0) throw new InvalidDataException("The satellite catalog is empty.");
        return new SatelliteCatalog(targets);
    }

    public IReadOnlyList<SatelliteTarget> Search(string query)
    {
        var needle = Normalize(query);
        if (needle.Length == 0) return Targets;
        var numeric = int.TryParse(needle, NumberStyles.None, CultureInfo.InvariantCulture, out var id);
        var idNeedle = numeric ? id.ToString(CultureInfo.InvariantCulture) : needle;
        var matches = Targets.Where(target => target.Key.Contains(needle, StringComparison.Ordinal) ||
            Normalize(target.Name).Contains(needle, StringComparison.Ordinal) ||
            CanonicalId(target.IlrsId).Contains(idNeedle, StringComparison.Ordinal) ||
            (target.NoradId is not null && CanonicalId(target.NoradId).Contains(idNeedle, StringComparison.Ordinal))).ToArray();
        return matches.Length > 0 ? matches : Targets.Where(target =>
            IsSubsequence(needle, target.Key) || IsSubsequence(needle, Normalize(target.Name))).ToArray();
    }

    private static bool IsDecimal(string value) => value.All(char.IsAsciiDigit) &&
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    private static string CanonicalId(string value) => int.Parse(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    private static string Normalize(string value) => new(value.Where(c => c != '-' && c != '_' && !char.IsWhiteSpace(c)).Select(char.ToLowerInvariant).ToArray());
    private static bool IsSubsequence(string needle, string value)
    {
        var matched = 0;
        foreach (var character in value)
            if (matched < needle.Length && needle[matched] == character) matched++;
        return matched == needle.Length;
    }
}

public static class TargetSelectionStore
{
    private sealed record Selection(int SchemaVersion, string[] Keys);
    public static void Load(string path, SatelliteCatalog catalog)
    {
        if (!File.Exists(path)) return;
        var selection = JsonSerializer.Deserialize<Selection>(File.ReadAllText(path));
        if (selection is null || selection.SchemaVersion != 1 || selection.Keys is null)
            throw new InvalidDataException("The saved target selection has an unsupported format.");
        var keys = selection.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var target in catalog.Targets) target.IsSelected = keys.Contains(target.Key);
    }

    public static void Save(string path, SatelliteCatalog catalog)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(new Selection(1, catalog.Targets.Where(t => t.IsSelected).Select(t => t.Key).ToArray()));
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
