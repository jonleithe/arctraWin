using System.Text.Json;
using System.ComponentModel;
using ArctraWin.App.Catalog;
using ArctraWin.App.Observer;
using ArctraWin.App.Live;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace ArctraWin.App;

public sealed partial class MainWindow : Window
{
    private bool initialized;
    private SatelliteCatalog? catalog;
    private bool globeReady;
    private LiveTrackingService? tracking;
    private bool closing;
    private ObserverSettings observer = ObserverSettings.Default;
    private bool updatingObserverInputs;
    private readonly string observerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArctraWin", "observer-v1.json");
    private readonly string selectionPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArctraWin", "selected-targets-v1.json");
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 940));
        Closed += (_, _) => { closing = true; tracking?.Dispose(); GlobeView.Close(); };
        LoadCatalog();
        LoadObserver();
    }

    private void LoadObserver()
    {
        string? error = null;
        try { observer = ObserverSettingsStore.Load(observerPath); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { error = $"Saved observer could not be loaded; using the default. {ex.Message}"; }
        FillObserverInputs();
        ObserverFeedback.Text = error ?? ObserverSummary();
        InitializeObserverProfiles();
        if (error is not null) ShowCatalogError(error);
    }
    private void FillObserverInputs(ObserverSettings? value = null)
    {
        var settings = value ?? observer;
        updatingObserverInputs = true;
        ObserverNameInput.Text = settings.Name;
        LatitudeInput.Value = settings.LatitudeDeg;
        LongitudeInput.Value = settings.LongitudeDeg;
        HeightInput.Value = settings.EllipsoidalHeightM;
        MinElevationInput.Value = settings.MinElevationDeg;
        PlaneWidthInput.Value = settings.PlaneWidthKm;
        ObserverPlaneToggle.IsChecked = settings.ShowPlane;
        ObserverConeToggle.IsChecked = settings.ShowElevationCone;
        updatingObserverInputs = false;
    }
    private async void EditObserver_Click(object sender, RoutedEventArgs e)
    {
        editMode = "edit";
        await ShowObserverEditorAsync(observer, activeProfile is null ? "Observer location" : $"Edit {activeProfile}", "Save");
    }
    private void ApplyObserver_Click(Microsoft.UI.Xaml.Controls.ContentDialog sender, Microsoft.UI.Xaml.Controls.ContentDialogButtonClickEventArgs e)
    {
        try
        {
            var next = new ObserverSettings(ObserverNameInput.Text, LatitudeInput.Value, LongitudeInput.Value,
                HeightInput.Value, PlaneWidthInput.Value, ObserverPlaneToggle.IsChecked == true,
                MinElevationInput.Value, ObserverConeToggle.IsChecked == true).Validated();
            var filename = editMode == "edit" && activeProfile is not null ? activeProfile : profiles.UniqueFilename(next.Name);
            profiles.Save(filename, next, overwrite: editMode == "edit" && activeProfile is not null);
            observer = next;
            ReloadProfiles(filename);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { ObserverValidationText.Text = ex.Message; e.Cancel = true; }
    }
    private void SaveObserver()
    {
        try { ObserverSettingsStore.Save(observerPath, observer); ObserverFeedback.Text = ObserverSummary() + " · saved"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ObserverFeedback.Text = $"Observer applied for this session; could not save: {ex.Message}"; }
    }
    private string ObserverSummary() => $"{observer.Name} · minimum elevation {observer.MinElevationDeg:0.##}°\n{observer.LatitudeDeg:F4}°, {observer.LongitudeDeg:F4}° · {observer.EllipsoidalHeightM:F0} m";
    private void SendObserver()
    {
        if (!globeReady) return;
        GlobeView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { command = "set-observer", observer },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
    private void ObserverPlane_Changed(object sender, RoutedEventArgs e)
    {
        if (updatingObserverInputs || !globeReady) return;
        observer = observer with { ShowPlane = ObserverPlaneToggle.IsChecked == true };
        SendObserver();
        SaveObserver();
    }
    private void ObserverSide_Click(object sender, RoutedEventArgs e) => Send("observer-side");

    private void UpdatePreview()
    {
        if (catalog is null) return;
        var selected = catalog.Targets.Where(t => t.IsSelected).ToArray();
        foreach (var target in catalog.Targets)
            target.SetLiveState(target.IsSelected ? "Loading current CPF…" : null, null);
        PreviewStatusText.Text = selected.Length == 0 ? "Select targets to load current CPF predictions." : "Acquiring and validating current CPF predictions…";
        PreviewButton.IsEnabled = ReferenceOrbitToggle.IsEnabled = TrajectoryToggle.IsEnabled = FrameTrajectoryButton.IsEnabled = false;
        if (!globeReady || tracking is null) return;
        tracking.SetSelection(selected);
        GlobeView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { command = "begin-live", keys = selected.Select(t => t.Key).ToArray() }));
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e) => Send("trajectory-view");
    private void FrameTrajectory_Click(object sender, RoutedEventArgs e) => Send("trajectory-view");
    private void TrajectoryToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (globeReady) Send(TrajectoryToggle.IsChecked == true ? "trajectory-on" : "trajectory-off");
    }
    private void ReferenceOrbitToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (globeReady) Send(ReferenceOrbitToggle.IsChecked == true ? "reference-on" : "reference-off");
    }

    private void LoadCatalog()
    {
        CatalogSourceText.Text = SatelliteCatalog.SnapshotLabel;
        try
        {
            catalog = SatelliteCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Catalog", "edc-active-satellites.csv"));
            try { TargetSelectionStore.Load(selectionPath, catalog); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            { ShowCatalogError($"Saved selection could not be restored: {ex.Message}"); }
            foreach (var target in catalog.Targets) target.PropertyChanged += Target_PropertyChanged;
            RefreshTargetList();
            UpdateSelectedTargets();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ShowCatalogError($"Catalog could not be loaded: {ex.Message}");
            TargetSearch.IsEnabled = false;
            CatalogCountText.Text = "Catalog unavailable";
        }
    }

    private void RefreshTargetList()
    {
        if (catalog is null) return;
        var matches = catalog.Search(TargetSearch.Text);
        TargetList.ItemsSource = matches;
        NoTargetsText.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CatalogCountText.Text = $"{matches.Count} of {catalog.Targets.Count} targets · {catalog.Targets.Count(t => t.IsSelectable)} CPF candidates";
    }

    private void TargetSearch_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e) => RefreshTargetList();
    private void Target_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SatelliteTarget.IsSelected) || catalog is null) return;
        UpdateSelectedTargets();
        try { TargetSelectionStore.Save(selectionPath, catalog); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ShowCatalogError($"Selection works for this session but could not be saved: {ex.Message}"); }
    }

    private void UpdateSelectedTargets()
    {
        if (catalog is null) return;
        var selected = catalog.Targets.Where(t => t.IsSelected).ToArray();
        SelectedCountText.Text = $"{selected.Length} selected";
        SelectedTargetsText.Text = selected.Length == 0 ? "Choose targets above." : string.Join(", ", selected.Select(t => t.Name));
        ClearTargetsButton.IsEnabled = selected.Length > 0;
        UpdatePreview();
    }

    private void ClearTargets_Click(object sender, RoutedEventArgs e)
    {
        if (catalog is null) return;
        foreach (var target in catalog.Targets) target.IsSelected = false;
    }

    private void ShowCatalogError(string message)
    {
        CatalogError.Message = message;
        CatalogError.IsOpen = true;
    }

    private async void GlobeView_Loaded(object sender, RoutedEventArgs e)
    {
        if (initialized) return;
        initialized = true;
        try
        {
            var globePath = Path.Combine(AppContext.BaseDirectory, "Globe");
            if (!File.Exists(Path.Combine(globePath, "index.html")))
                throw new FileNotFoundException("Globe assets are missing. Run scripts/build.ps1 and restart.");
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArctraWin", "WebView2");
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", profile);
            await GlobeView.EnsureCoreWebView2Async();
            GlobeView.CoreWebView2.SetVirtualHostNameToFolderMapping("globe.arctrawin.local", globePath, CoreWebView2HostResourceAccessKind.DenyCors);
            GlobeView.CoreWebView2.WebMessageReceived += OnWebMessage;
            GlobeView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) || uri.Host != "globe.arctrawin.local") args.Cancel = true;
            };
            GlobeView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess) ShowError($"Globe navigation failed: {args.WebErrorStatus}");
            };
            GlobeView.Source = new Uri("https://globe.arctrawin.local/index.html");
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var message = JsonDocument.Parse(args.WebMessageAsJson);
            var type = message.RootElement.GetProperty("type").GetString();
            if (type == "ready")
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                globeReady = true;
                StatusText.Text = "Globe ready · connecting live CPF backend";
                EarthButton.IsEnabled = ObserverButton.IsEnabled = GridToggle.IsEnabled = true;
                ObserverSideButton.IsEnabled = true;
                SendObserver();
                StartLiveTracking();
                UpdatePreview();
            }
            else if (type == "trajectory-error")
            {
                PreviewStatusText.Text = $"Trajectory rejected: {message.RootElement.GetProperty("message").GetString()}";
            }
            else if (type is "live-timeout" or "live-error")
            {
                LiveBadge.Text = "LIVE · INTERRUPTED";
                StatusText.Text = "Live updates interrupted · positions hidden";
                PreviewStatusText.Text = message.RootElement.TryGetProperty("message", out var reason) ? reason.GetString() : "Waiting for a fresh live CPF state.";
                PreviewButton.IsEnabled = ReferenceOrbitToggle.IsEnabled = TrajectoryToggle.IsEnabled = FrameTrajectoryButton.IsEnabled = false;
                foreach (var target in catalog?.Targets.Where(t => t.IsSelected) ?? []) target.SetLiveState("Updates interrupted", "Waiting for a fresh current CPF state.");
            }
            else if (type == "observer-error") ObserverFeedback.Text = $"Observer rejected: {message.RootElement.GetProperty("message").GetString()}";
            else if (type == "error") ShowError(message.RootElement.GetProperty("message").GetString() ?? "Renderer failed.");
        }
        catch (JsonException) { ShowError("The globe sent an invalid status message."); }
    }

    private void ShowError(string message)
    {
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingRing.IsActive = false;
        LoadingText.Text = message;
        StatusText.Text = "Globe unavailable";
    }
    private void Send(string command) => GlobeView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new { command }));
    private void GlobalView_Click(object sender, RoutedEventArgs e) => Send("global");
    private void ObserverView_Click(object sender, RoutedEventArgs e) => Send("observer");
    private void GridToggle_Toggled(object sender, RoutedEventArgs e) => Send(GridToggle.IsOn ? "grid-on" : "grid-off");
}
