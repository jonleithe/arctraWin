using ArctraWin.App.Observer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using Windows.Storage.Pickers;

namespace ArctraWin.App;

public sealed partial class MainWindow
{
    private readonly ObserverProfileStore profiles = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArctraWin", "Observers"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArctraWin", "observer-profile.json"));
    private string? activeProfile;
    private bool updatingProfiles;
    private string editMode = "edit";

    private void InitializeObserverProfiles()
    {
        try {
            profiles.Initialize(observer, File.Exists(observerPath));
            string? preferred = null;
            try { preferred = profiles.ReadSelection(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException) { ShowCatalogError(ex.Message); }
            ReloadProfiles(preferred);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) {
            ObserverFeedback.Text = $"Observer profiles unavailable; using current location. {ex.Message}";
        }
    }
    private void ReloadProfiles(string? preferred)
    {
        var items = profiles.Load(observer, out var errors);
        var selected = items.FirstOrDefault(p => p.Filename.Equals(preferred, StringComparison.OrdinalIgnoreCase)) ?? items.FirstOrDefault();
        updatingProfiles = true;
        ObserverSelector.ItemsSource = items;
        ObserverSelector.SelectedItem = selected;
        updatingProfiles = false;
        if (selected is not null) ActivateProfile(selected);
        else ObserverFeedback.Text = "No valid observer profiles. Create or import an observer INI file.";
        if (errors.Length > 0) ShowCatalogError("Observer files skipped: " + string.Join("; ", errors.Take(3)));
        if (preferred is not null && selected is not null && !selected.Filename.Equals(preferred, StringComparison.OrdinalIgnoreCase))
            ShowCatalogError($"Observer profile '{preferred}' is missing or invalid; selected '{selected.Filename}'.");
    }
    private void ActivateProfile(ObserverProfile profile)
    {
        observer = (profile.Settings with { PlaneWidthKm = observer.PlaneWidthKm, ShowPlane = observer.ShowPlane,
            ShowElevationCone = observer.ShowElevationCone }).Validated();
        activeProfile = profile.Filename;
        FillObserverInputs();
        tracking?.SetObserver(observer);
        SendObserver();
        SaveObserver();
        try { profiles.Select(profile.Filename); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) {
            ObserverFeedback.Text = ObserverSummary() + $" · active choice could not be saved: {ex.Message}";
        }
    }
    private void ObserverSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingProfiles && ObserverSelector.SelectedItem is ObserverProfile profile) ActivateProfile(profile);
    }
    private void ReloadObservers_Click(object sender, RoutedEventArgs e)
    {
        try { ReloadProfiles(activeProfile); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowCatalogError(ex.Message); }
    }
    private async void ImportObserver_Click(object sender, RoutedEventArgs e)
    {
        try {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".ini");
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var imported = ObserverIni.Parse(await File.ReadAllTextAsync(file.Path), observer);
            var filename = profiles.UniqueFilename(Path.GetFileNameWithoutExtension(file.Name));
            profiles.Save(filename, imported, overwrite: false);
            ReloadProfiles(filename);
        } catch (Exception ex) { ShowCatalogError($"Observer import failed: {ex.Message}"); }
    }
    private async void NewObserver_Click(object sender, RoutedEventArgs e)
    {
        editMode = "new";
        await ShowObserverEditorAsync(observer with { Name = "New observer", LatitudeDeg = 0, LongitudeDeg = 0, EllipsoidalHeightM = 0, MinElevationDeg = 0 }, "New observer", "Create");
    }
    private async void SaveObserverAs_Click(object sender, RoutedEventArgs e)
    {
        editMode = "copy";
        await ShowObserverEditorAsync(observer with { Name = observer.Name + " copy" }, "Save observer as new profile", "Save copy");
    }
    private async void OpenObserverFolder_Click(object sender, RoutedEventArgs e)
    {
        try {
            if (!await Windows.System.Launcher.LaunchFolderPathAsync(profiles.DirectoryPath)) ShowCatalogError("Could not open the observer profile folder.");
        } catch (Exception ex) { ShowCatalogError(ex.Message); }
    }
    private async Task ShowObserverEditorAsync(ObserverSettings value, string title, string button)
    {
        FillObserverInputs(value);
        ObserverValidationText.Text = "";
        ObserverDialog.Title = title;
        ObserverDialog.PrimaryButtonText = button;
        ObserverDialog.XamlRoot = GlobeView.XamlRoot;
        await ObserverDialog.ShowAsync();
    }
    private void ObserverCone_Changed(object sender, RoutedEventArgs e)
    {
        if (updatingObserverInputs || !globeReady) return;
        observer = observer with { ShowElevationCone = ObserverConeToggle.IsChecked == true };
        SendObserver();
        SaveObserver();
    }
}
