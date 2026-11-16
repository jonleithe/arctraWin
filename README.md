# ArctraWin

Windows desktop companion for Arctra. The application combines a native
C# / WinUI 3 shell with an embedded CesiumJS/TypeScript globe in WebView2 while keeping CPF parsing,
interpolation, pass prediction, and geometric calculations in the authoritative
Arctra C++ backend.

The selected stack is recorded in [the application stack decision](docs/decisions/0001-application-stack.md).

This project is a visualization and engineering-analysis tool. It is not
telescope or laser-control software and must preserve Arctra's validity,
expiry, and no-extrapolation rules.

## Repository layout

- `src/ArctraWin.App/` — WinUI 3 desktop shell and native Windows integration.
- `src/ArctraWin.Backend/` — local service/adaptor around the Arctra C++ engine.
- `src/ArctraWin.Globe/` — CesiumJS/TypeScript globe rendered in WebView2.
- `contracts/schemas/` — versioned JSON schemas shared across process boundaries.
- `tests/` — app, backend, globe, and contract tests.
- `assets/` — icons and non-operational sample prediction snapshots.
- `docs/` — architecture, decisions, and developer documentation.
- `packaging/msix/` — Windows/MSIX packaging inputs.
- `scripts/` — repeatable development and packaging helpers.

The application now tracks current CPF predictions through a persistent local
C++ worker. Recorded snapshots remain reproducible development/test fixtures.

## Run the GUI

The initial GUI is implemented: a native WinUI 3 window embeds a Cesium globe
with bundled Natural Earth imagery, an approximate Ny-Ålesund marker, global
and observer camera presets, and a latitude/longitude grid. The native sidebar
also offers Arctra's 144-entry EDC catalog snapshot with search and multiple
target selection. Selected targets automatically acquire current CPF data from
EDC and update their positions and observer look angles every second. No Cesium
token or imagery connection is needed; live acquisition needs EDC access or a
validated, currently usable local CPF cache.

Choose a station in the **Observer** selector above the satellite list. Open
**Observer and tangent plane** to create, edit, import or copy station INI files,
including coordinates, WGS 84 ellipsoidal height and minimum elevation.
The active station is saved between launches.
The translucent plane and East/North/Up axes follow that location; use
**View plane from the side** or rotate the globe freely to inspect it.
The plane automatically reaches beyond the farthest selected loaded orbit.
Visibility and the fallback width are configurable. See [observer controls](docs/observer-plane.md).

Search by name, CPF key, NORAD ID or ILRS ID, including compact queries such as
`lgs1`. Selections survive searches and restarts and are stored under
`%LOCALAPPDATA%/ArctraWin/selected-targets-v1.json`. Six lunar entries are visible
but disabled. The 138 selectable entries are CPF candidates; current CPF
availability and supported headers are checked at runtime. See the
[catalog provenance](assets/catalog/README.md). Expand **Observer and tangent plane**
below the selection panel for the camera and grid controls.

Select targets, then click **Frame selected orbits** to frame their coloured
closed reference ellipses. **Closed reference orbits** toggles those loops;
**CPF trails** independently shows the original open Earth-fixed prediction
paths. Deselecting a target removes its geometry. Markers update every second;
reference loops/trails refresh each minute or when CPF data changes. Closed loops
are approximate two-body geometry, not the source of current positions. Green
markers meet the observer's configured minimum elevation, calculated by Arctra.
The gold cone shows that limit; the horizontal plane remains at 0°.
Loading, unavailable and expired targets are identified; stale/expired positions
are hidden. See [live tracking](docs/live-tracking.md) for cache and refresh rules.

The calculation and visualization distinction are documented in the
[closed reference-orbit decision](docs/decisions/0002-closed-reference-orbits.md).

Prerequisites: Windows 11 x64, Visual Studio with WinUI/Windows SDK and C++20
build tools, CMake,
.NET SDK 11, Node.js and npm, and the Microsoft Edge WebView2 Runtime.

From PowerShell in this repository:

```powershell
./build.bat debug
./run-debug.bat
```

Use `./build.bat release` for Release, or `./build.bat all` to build both Debug
and Release. With no argument it builds Debug. Close ArctraWin before rebuilding.
The batch file invokes the PowerShell build script with a process-local execution
policy override. You can also call `scripts/build.ps1 -Configuration All`,
`Debug`, or `Release` directly. `all` builds the shared globe assets once.
To launch the Release build, use `./run-release.bat`. Both run launchers can
also be double-clicked in File Explorer and work from any current directory.

The first build downloads dependencies and builds/tests the C++ worker. Globe
rendering uses local assets; the native app acquires CPF data in the background.
The application is currently unpackaged and includes its .NET and Windows App
SDK runtimes. WebView2 stores its profile under `%LOCALAPPDATA%/ArctraWin/WebView2`.

To check globe rendering and camera commands using installed Microsoft Edge:

```powershell
cd src/ArctraWin.Globe
npm.cmd test
```

The test checks that rendering makes no external requests and saves screenshots
in the ignored `test-results` directory. Run `npm.cmd run build` first after
changing globe sources. `ArctraWin.slnx` opens the native project in Visual Studio;
the build script also rebuilds and copies the globe assets.

Catalog, search, unsupported-target and selection-persistence checks run without
additional test dependencies:

```powershell
dotnet run --project tests/App.Tests/ArctraWin.Catalog.Tests.csproj
```
