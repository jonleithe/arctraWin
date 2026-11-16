using ArctraWin.App.Catalog;
using ArctraWin.App.Live;

if (args.Length > 0 && args[0] == "--live-integration")
{
    await LiveIntegration.RunAsync(args[1], args[2]);
    return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Reject(Action action, string message)
{
    try { action(); }
    catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return; }
    throw new Exception(message);
}

var catalog = SatelliteCatalog.Load(Path.Combine(AppContext.BaseDirectory, "catalog.csv"));
Check(catalog.Targets.Count == 144, "The full Arctra catalog must be bundled.");
Check(catalog.Targets.Count(t => t.IsSelectable) == 138, "Lunar targets must remain catalog-only.");
var lageos = catalog.Targets.Single(t => t.Key == "lageos1");
Check(lageos.NoradId == "08820", "Preserve the original zero-padded NORAD ID.");
Check(catalog.Search("LAGEOS - 1").Single() == lageos, "Normalize spaces, case and hyphens like Arctra.");
Check(catalog.Search("lgs1").Contains(lageos), "Support Arctra's compact fuzzy query.");
Check(catalog.Search("08820").Contains(lageos) && catalog.Search("8820").Contains(lageos), "Search numeric IDs with or without leading zeros.");
Check(catalog.Search("7603901").Contains(lageos), "Search ILRS IDs.");
Check(catalog.Search("galileo").Count == 32, "Search by target family.");
Check(catalog.Search("not-a-real-target-xyz").Count == 0, "Unknown searches must have no matches.");
Check(catalog.Search("  ").Count == 144, "Clearing the search restores the full catalog.");

lageos.IsSelected = true;
Check(catalog.Search("galileo").All(t => t.Key != lageos.Key) && lageos.IsSelected, "Searching must retain hidden selections.");
var lunar = catalog.Targets.Single(t => t.Key == "apollo11");
lunar.IsSelected = true;
Check(!lunar.IsSelected, "Lunar targets cannot be selected, including through restored settings.");
var changeCount = 0;
lageos.PropertyChanged += (_, _) => changeCount++;
lageos.IsSelected = true;
lageos.IsSelected = false;
Check(changeCount == 1, "Only actual changes should notify bindings.");
lageos.IsSelected = true;

var temporaryDirectory = Path.Combine(Path.GetTempPath(), "ArctraWin-CatalogTests-" + Guid.NewGuid());
Directory.CreateDirectory(temporaryDirectory);
var path = Path.Combine(temporaryDirectory, "selection.json");
try
{
    TargetSelectionStore.Save(path, catalog);
    var restored = SatelliteCatalog.Load(Path.Combine(AppContext.BaseDirectory, "catalog.csv"));
    TargetSelectionStore.Load(path, restored);
    Check(restored.Targets.Where(t => t.IsSelected).Select(t => t.Key).SequenceEqual(["lageos1"]), "Selections must survive restart.");
    File.WriteAllText(path, "{\"SchemaVersion\":1,\"Keys\":[\"missing-target\",\"apollo11\",\"lageos2\"]}");
    TargetSelectionStore.Load(path, restored);
    Check(restored.Targets.Where(t => t.IsSelected).Select(t => t.Key).SequenceEqual(["lageos2"]), "Ignore unknown and unsupported saved keys.");
    File.WriteAllText(path, "{\"SchemaVersion\":99,\"Keys\":[]}");
    Reject(() => TargetSelectionStore.Load(path, restored), "Reject unsupported settings versions.");
    File.WriteAllText(path, "invalid json");
    Reject(() => TargetSelectionStore.Load(path, restored), "Report corrupt settings.");
    foreach (var target in catalog.Targets) target.IsSelected = false;
    TargetSelectionStore.Save(path, catalog);
    TargetSelectionStore.Load(path, restored);
    Check(restored.Targets.All(t => !t.IsSelected), "Clearing must be saved.");
}
finally
{
    foreach (var file in Directory.EnumerateFiles(temporaryDirectory)) File.Delete(file);
    Directory.Delete(temporaryDirectory);
}

const string header = "cpf_key,name,ilrs_id,norad_id\n";
Reject(() => SatelliteCatalog.Parse(header), "Reject empty catalogs.");
Reject(() => SatelliteCatalog.Parse("wrong header\n"), "Reject unknown formats.");
Reject(() => SatelliteCatalog.Parse(header + "foo,Foo,1234567,22\nfoo,Duplicate,1234568,23"), "Reject duplicate keys.");
Reject(() => SatelliteCatalog.Parse(header + "foo,Foo,bad,22"), "Reject invalid IDs.");
Reject(() => SatelliteCatalog.Parse(header + "Foo,Foo,1234567,22"), "Reject noncanonical keys.");
Console.WriteLine("Catalog checks passed: 144 targets, search, unsupported targets, filtering, selection persistence, and invalid input.");
ObserverTests.Run();
ObserverProfilesTests.Run();
var listing = "<a href='/pub/slr/cpf_predicts_v2/current/lageos1_cpf_261115_31902.hts'>a</a><a href='lageos1_cpf_261115_31902.dgf'>b</a><a href='lageos2_cpf_261116_32001.dgf'>c</a>";
Check(CpfRefreshPolicy.SelectFilename("lageos1", listing) == "lageos1_cpf_261115_31902.dgf", "Same-issue provider preference must match Arctra.");
Check(CpfRefreshPolicy.SelectFilename("lares", listing) is null, "Do not confuse target keys.");
Check(CpfRefreshPolicy.SelectFilename("lageos1", listing + "<a href='lageos1_cpf_261116_32001.sgf'>new</a>") == "lageos1_cpf_261116_32001.sgf", "Prefer newer issues before provider rank.");
var refreshNow = DateTimeOffset.Parse("2026-11-16T09:00:00Z");
Check(CpfRefreshPolicy.NextCheck(refreshNow, refreshNow.AddHours(6), true) == refreshNow.AddHours(1), "Normal CPF refresh is hourly.");
Check(CpfRefreshPolicy.NextCheck(refreshNow, refreshNow.AddMinutes(45), true) == refreshNow.AddMinutes(15), "Check thirty minutes before expiry.");
Check(CpfRefreshPolicy.NextCheck(refreshNow, refreshNow.AddMinutes(10), false) == refreshNow.AddMinutes(5), "Retry failed refresh in five minutes.");
Console.WriteLine("Live acquisition filename and refresh policy checks passed.");
