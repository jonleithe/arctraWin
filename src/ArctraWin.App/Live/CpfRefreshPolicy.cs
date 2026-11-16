using System.Text.RegularExpressions;

namespace ArctraWin.App.Live;

public static class CpfRefreshPolicy
{
    public const string DirectoryUrl = "https://edc.dgfi.tum.de/pub/slr/cpf_predicts_v2/current/";
    public static string? SelectFilename(string key, string listing)
    {
        var pattern = new Regex("^" + Regex.Escape(key) + @"_cpf_([0-9]{6}_[0-9]{3,8})\.([a-z0-9]+)$", RegexOptions.CultureInvariant);
        return Regex.Matches(listing, "href=['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase)
            .Select(match => Uri.TryCreate(new Uri(DirectoryUrl), match.Groups[1].Value, out var uri) ? Path.GetFileName(uri.AbsolutePath) : "")
            .Select(name => (Name: name, Match: pattern.Match(name)))
            .Where(item => item.Match.Success)
            .OrderByDescending(item => item.Match.Groups[1].Value, StringComparer.Ordinal)
            .ThenByDescending(item => item.Match.Groups[2].Value switch { "dgf" => 3, "hts" => 2, _ => 1 })
            .Select(item => item.Name).FirstOrDefault();
    }
    public static DateTimeOffset NextCheck(DateTimeOffset now, DateTimeOffset coverageEnd, bool success)
    {
        if (!success) return now.AddMinutes(5);
        var nearExpiry = coverageEnd.AddMinutes(-30);
        return nearExpiry <= now ? now.AddMinutes(5) : (nearExpiry < now.AddHours(1) ? nearExpiry : now.AddHours(1));
    }
}
