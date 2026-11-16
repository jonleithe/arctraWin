# ArctraWin project handover

Last updated: 2026-11-16

## Purpose

ArctraWin is a separate Windows desktop companion for the C++20
[`arctra`](https://github.com/jonleithe/arctra) project. Its intended experience is an interactive,
Google-Earth-like tracker for CPF-predicted Satellite Laser Ranging targets,
with a native Windows application shell, selectable satellites, observer
sites, trajectories, visibility information, and detailed geometric data.

The decision to create this application came from considering native Windows
support for Arctra. Porting Arctra's POSIX terminal dashboard would require a
Windows terminal backend, but would still reproduce a terminal interface.
ArctraWin instead directs that effort toward the richer graphical tracker that
was discussed earlier and recorded in Arctra's
`docs/arctra-globe-handoff.md`.

ArctraWin is not intended to replace Arctra. Arctra remains the transparent,
testable prediction engine and command-line engineering tool.

## Current repository status

- Repository root: `/home/leijon/projects/arctraWin`
- GitHub repository: `https://github.com/jonleithe/arctraWin`
- Git remote: `git@github-jonleithe:jonleithe/arctraWin.git`
- Default branch: `master`, tracking `origin/master`
- Initial scaffold commit: `69b1d2c` (`Initial commit`)
- The first native GUI and offline Cesium globe are implemented. Build/run
  instructions are in README.md. Prediction snapshots and backend integration
  are not yet implemented.

## Windows GUI checkpoint (2026-11-16)

- Current Windows checkout: `C:\Users\jole\projects\arctraWin`.
- Native shell: C# / WinUI 3, .NET 11, Windows App SDK 2.6.5, x64,
  unpackaged with self-contained .NET and Windows App SDK runtimes.
- Globe: Cesium 1.147.0, TypeScript 7.0.2, Vite 8.3.8, pinned with npm lockfile.
- Bundled Natural Earth imagery and ellipsoid terrain; no Ion token required.
- WebView2 maps bundled assets to `https://globe.arctrawin.local`.
- Native controls send camera/grid commands; renderer reports ready/errors.
- Approximate Ny-Ålesund marker only; no invented satellite positions.
- Build: `./scripts/build.ps1`. Launch: `./scripts/run.ps1`.
- Rendering smoke test: `npm.cmd test` in `src/ArctraWin.Globe` after building.
- Next: versioned snapshot schema and authoritative Arctra sample data.

### Target selection checkpoint

- The native sidebar now loads the same 144-entry EDC catalog snapshot as
  Arctra, taken on 2026-11-13. Source commit and provenance are recorded in
  `assets/catalog/README.md`; the CSV is bundled with the app.
- Search follows Arctra's case/separator normalization, numeric ID handling,
  and fuzzy subsequence fallback. Selection survives filtering.
- 138 Earth-orbiting CPF candidates can be selected; six lunar entries are
  visible but disabled. Catalog membership does not establish CPF availability.
- Selected CPF keys persist in `%LOCALAPPDATA%/ArctraWin/selected-targets-v1.json`.
- The globe does not receive synthetic positions from catalog selection.
- Pure C# checks: `dotnet run --project tests/App.Tests/ArctraWin.Catalog.Tests.csproj`.
- Next: acquire and validate CPF through Arctra, expose the versioned snapshot,
  and display selected targets only within valid prediction coverage.

### CPF trajectory checkpoint

- Downloaded the complete real EDC/DGF `lageos1_cpf_261115_31902.dgf`;
  source and retrieval time are in `assets/sample-data/cpf/source.json`.
- A pinned, unchanged subset of Arctra C++ code builds on Windows with MSVC
  through `src/ArctraWin.Backend/CMakeLists.txt`. It excludes the POSIX terminal
  and network-acquisition code. Pin details are in `vendor/arctra/README.md`.
- The exporter validates identity/frame, enforces CPF intended AND record
  coverage, rejects gaps and leap flags, and samples Arctra interpolation.
- `scripts/export-sample.ps1` builds/tests the exporter and creates 481 samples
  over 2026-11-16 09:00–13:00 UTC at 30-second spacing.
- The narrower `trajectory-snapshot-v1.schema.json` is a recorded single-target
  preview contract, not the future observer/pass/live-state snapshot.
- Native selection gates LAGEOS-1's data; its preview button also frames the
  path. The globe validates the schema, coverage, ordering and metre units,
  draws an unclamped Cartesian polyline, and never closes or extends it.
- Native build, C++ boundary tests, catalog checks and three Playwright
  contract/rendering tests passed. Full native UI visual check is also required
  when changing deployment or WebView message handling.
- Still pending: live acquisition/service, automatic refresh, additional target
  snapshots, observer visibility and independent operational validation.

### Rendering refinement

- Removed the star-texture skybox and reduced ground-atmosphere washout.
- Added camera-relative illustrative globe shading (not physical sunlight),
  adjusted map contrast, and enabled FXAA.
- Frame-path uses an oblique, viewport-aware camera fit; predictions are unchanged.
- Simplified labels and preview timestamps, marked both supplied endpoints,
  and improved checkbox/content spacing in the native target list.

### Closed reference-orbit checkpoint

- User clarified that selected satellites should show full closed loops.
- Added separate, explicitly approximate two-body reference ellipses in C++,
  derived from CPF positions and differentiated motion with nominal Earth
  rotation. Upstream Arctra interpolation remains unchanged. Details and limits
  are in `docs/decisions/0002-closed-reference-orbits.md`.
- The snapshot schema accepts optional `referenceOrbit` with epoch-frozen axes
  and 361 vertices; first/last match the CPF marker exactly. This geometry has
  no prediction timestamps and never replaces the original CPF samples.
- Downloaded additional SGF CPF files for LAGEOS-2, LARES, and LARES-2; source
  URLs and retrieval timestamps are in `cpf/additional-sources.json`.
- Export script now produces all four target fixtures at the same recorded
  epoch. The app loads them by catalog identity and sends selected snapshots.
- Closed loops are enabled by default, CPF trails separately disabled by
  default. Targets are colour-coded and deselection removes their geometry.
- Targets outside the four bundled fixtures remain selectable but explicitly
  have no loaded data. Live acquisition is still pending.

### Observer tangent-plane checkpoint

- Added a native observer coordinate dialog: name, geodetic latitude/longitude,
  WGS 84 ellipsoidal height, and plane display width. Apply validates all values;
  Cancel leaves the active observer unchanged.
- Coordinates, plane width and visibility persist in
  `%LOCALAPPDATA%/ArctraWin/observer-v1.json`; malformed saved settings fall back
  visibly to approximate Ny-Ålesund defaults.
- Cesium renders a translucent Earth-fixed local East–North plane, border/grid,
  E/N/Up axes and a relocated observer marker. Surface ENU determines orientation;
  configured height only translates the geometry.
- Top/side camera presets use the active observer and release their local
  camera transform afterwards. Free globe rotation continues to work.
- This is rendering geometry only. Authoritative look angles and satellite
  visibility remain backend work; terrain/obstructions are intentionally omitted.
- Details and controls are in `docs/observer-plane.md`.

### Live tracking checkpoint

- Default GUI mode now uses current CPF predictions rather than bundled recorded
  JSON. Selected supported targets are acquired asynchronously from EDC.
- A persistent `arctrawin-live.exe` worker exchanges version-1 JSON lines over
  private stdin/stdout. It uses unchanged pinned Arctra CPF/coordinates code,
  caches parsed files, checks identities and coverage, and computes current
  positions and observer azimuth/elevation/range once per second.
- C# owns the serialized HTTP acquisition queue, provider-issue selection,
  validated temporary-file promotion, source/retrieval sidecars and cache.
  Refresh is hourly or thirty minutes before expiry, with five-minute retries.
  Refresh failure retains valid loaded data until usable coverage ends.
- Live positions and observer look angles update every second. Approximate
  reference loops and forward CPF trails refresh each minute or on file/selection
  changes. Trails never extend beyond CPF coverage.
- Rows display CPF readiness and current elevation; tooltips retain provenance,
  coverage and geometry. Green markers mean above the ideal 0° horizon.
- Renderer uses `live-state-v1.schema.json` plus freshness/coverage checks, hides
  interrupted state after about 3.5 seconds, and does not extrapolate. Selection
  revisions discard late results; closing the window stops the owned worker.
- Normal build now builds/tests and deploys the static-runtime C++ worker.
- Verified real EDC acquisition for LAGEOS-1/2 and LARES/2, consecutive advancing
  UTC seconds, observer changes, partial deselection and clearing selection.
  Details: `docs/live-tracking.md`, transport ADR 0003, and `--live-integration`
  in the C# check executable.
- Recorded fixtures remain test/development artifacts. Remaining work includes
  pass countdowns, minimum-elevation configuration, independent operational
  accuracy comparison and packaging/install validation.

### Automatic horizontal-plane extent

- The observer plane now fits the horizontal projection of selected loaded
  reference orbits and CPF trails, with an 8% margin rounded upward to 100 km.
- Automatic dimensions are not restricted by the old 40000 km manual limit.
  Removing distant targets shrinks the plane; changing observer coordinates
  recomputes bounds in the new local frame. Live failures/expired targets do not
  retain an obsolete large-plane extent.
- The persisted `planeWidthKm` value remains compatible and acts as the fallback
  when no target data is loaded. The editor label reflects that role.
- Camera presets use the actual rendered width. Sizing does not change
  orientation, satellite positions, prediction calculations or visibility.

The project is deliberately an independent Git repository beside Arctra. It
can later be opened with Arctra in a VS Code multi-root workspace, but that
workspace configuration has been deferred.

## Recommended architecture

The current recommendation is:

```text
ArctraWin desktop application
├── WinUI 3 native Windows shell
│   ├── windows, menus, settings and target selection
│   ├── detailed status and prediction panels
│   └── WebView2 host
├── CesiumJS/TypeScript globe in WebView2
│   ├── WGS 84 globe and camera controls
│   ├── satellite markers and labels
│   ├── sampled 3D trajectories and ground tracks
│   └── explanatory visibility overlays
└── Arctra C++ backend/adaptor
    ├── CPF acquisition and validation
    ├── interpolation and coverage enforcement
    ├── ECEF/ENU geometry
    ├── pass and threshold-event prediction
    └── provenance, refresh and expiry policy
```

WinUI 3 plus WebView2 makes this a native, packageable Windows desktop
application while using CesiumJS for the mature globe rendering. Implementing
a comparable ellipsoidal globe, camera, labels, and trajectories directly in
DirectX would add substantial rendering work without improving the prediction
mathematics.

The selected stack is C# / WinUI 3 for the native shell, CesiumJS with
TypeScript/JavaScript in WebView2 for globe rendering, and C++ for the Arctra
backend. See [the accepted application stack decision](docs/decisions/0001-application-stack.md).

## Authority and component boundaries

Arctra's C++ calculations must remain authoritative. In particular:

- The globe must not parse CPF files.
- TypeScript must not reimplement interpolation, ECEF/ENU transformations,
  pass finding, coverage limits, or target validation.
- The UI may render state, request selections, and control the timeline, but
  it must not manufacture positions or events.
- ArctraWin should consume an explicit, versioned data contract rather than
  share internal C++ objects across the UI boundary.

This separation keeps visual behavior independently replaceable and prevents
the Windows application from becoming a second, subtly different orbital
implementation.

## Backend integration strategy

Three integration options were considered:

1. **Local service with versioned JSON** — recommended for the first live
   implementation. Communication could use localhost HTTP/WebSocket or Windows
   named pipes. This cleanly separates the UI from C++ lifetimes and threading.
2. **C-compatible DLL API** — a possible later optimization, but it introduces
   ABI, ownership, threading, deployment, and versioning concerns early.
3. **Exporter executable and snapshot files** — best for the first offline
   proof, but not sufficient by itself for a polished once-per-second tracker.

Development should therefore begin with a static versioned JSON snapshot,
then introduce the local service only after the contract and offline globe are
useful and tested.

## Data contract

Schemas belong in `contracts/schemas/`. The first snapshot contract should
include at least:

- schema version and generation epoch;
- explicit prediction kind, such as instantaneous geometric CPF/ECEF;
- observatory identifier, name, geodetic latitude/longitude, ellipsoidal
  height, and minimum elevation;
- target key, display name, NORAD ID, and ILRS satellite ID;
- CPF provider, exact filename, retrieval metadata, and usable coverage;
- current epoch and ECEF position in metres;
- geometric azimuth/elevation in degrees and slant range in metres;
- above-threshold state, kept distinct from CPF validity;
- valid sampled trajectory points with explicit epochs;
- next threshold event where available, without inventing one beyond coverage;
- errors and unsupported states in machine-readable form.

Use ISO-8601 UTC timestamps at the interface. Units must be encoded in field
names or schema descriptions. Only valid samples may be transferred; a client
must never extend a trajectory beyond CPF coverage.

## Intended user experience

The application should eventually provide:

- a searchable SLR target catalog and editable priorities;
- an interactive WGS 84 globe with mouse rotate, pan, and zoom;
- satellite markers at CPF-predicted Earth-fixed positions;
- labels showing compact visibility and prediction-health state;
- one or more observer locations, beginning with Ny-Ålesund;
- separate toggles for the 3D spatial trajectory and surface ground track;
- target details for geometric azimuth, elevation, range, round-trip vacuum
  time, CPF expiry, and next rise/set when available;
- a polar sky view related to Arctra's terminal tracker sky plot;
- live, paused, and forecast timeline modes;
- clear distinction between geometric visibility and CPF validity;
- diagnostics that retain the exact CPF provider/file and coverage.

An arbitrary point clicked on the globe may later become a temporary geometric
observer. It must have explicit assumed height and elevation threshold and
must not be presented as a surveyed or operational station.

## Trajectories and visibility overlays

Draw the actual sampled CPF/ECEF trajectory as a 3D polyline. Do not substitute
an invented Keplerian orbit. A ground track is the sequence of satellite
subpoints on the Earth and is a different visualization from the spatial
trajectory; the UI should not conflate them.

A fixed horizon circle around an observer is only an explanatory planning aid.
Its useful surface extent depends on satellite altitude and the minimum
elevation threshold. Arctra's exact geometric ECEF-to-ENU elevation remains the
authoritative visibility test. A later instantaneous visibility footprint may
be derived for the selected target, but it must not replace that test.

## Safety and mathematical limits

These limits must remain prominent in UI text, documentation, and data models:

- ArctraWin displays CPF-based geometric predictions, not live spacecraft
  telemetry.
- It is not certified telescope or laser-control software.
- It is not an operational pointing or ranging solution.
- No state, trajectory, countdown, or event may be extrapolated beyond valid
  CPF coverage.
- Current Arctra support is for passive Earth-orbiting CPF v2 targets in the
  supported geocentric true body-fixed frame-0 path.
- Lunar reflectors require transmit/return light-time legs and lunar
  orientation treatment and are not supported by the current Earth-orbit path.
- Displayed round-trip microseconds are `2 * geometric range / c` in vacuum,
  not an iterated or corrected SLR light-time solution.
- Current output omits atmospheric refraction, terrain/obstruction masks,
  station displacement, instrumentation corrections, and safety interlocks.
- Arctra's present UTC representation cannot preserve a distinct leap-second
  label and must not be claimed reliable across a leap second.
- Visual precision and displayed decimal places do not establish validated
  accuracy. Exact-epoch comparison against independent operational
  predictions remains important.

## Repository layout

```text
arctraWin/
├── README.md
├── HANDOVER.md
├── src/
│   ├── ArctraWin.App/       WinUI 3 shell and Windows integration
│   ├── ArctraWin.Backend/   local C++ service/adaptor
│   └── ArctraWin.Globe/     CesiumJS/TypeScript WebView2 application
├── contracts/
│   └── schemas/             versioned JSON schemas
├── tests/
│   ├── App.Tests/
│   ├── Backend.Tests/
│   ├── Globe.Tests/
│   └── Contract.Tests/
├── assets/
│   ├── sample-data/         labelled, reproducible offline fixtures
│   └── icons/
├── docs/
│   └── decisions/           architecture decision records
├── packaging/
│   └── msix/                manifests and MSIX inputs
└── scripts/                 setup, build, test and packaging helpers
```

The empty component directories exist locally. Add a small README or real
source file when work starts so Git can track them.

## Suggested milestones

### Milestone A — offline visual proof

- Use the accepted C# / WinUI 3 shell decision.
- Create the WinUI 3 application and WebView2 host.
- Create the CesiumJS/TypeScript globe application.
- Load a checked-in, clearly labelled JSON snapshot.
- Show a WGS 84 globe, Ny-Ålesund observer, selected satellite markers,
  labels, and 30–60 minutes of valid sampled trajectory.
- Support target selection and normal globe camera controls.
- Do not add networking, CPF parsing, or arbitrary-location geometry yet.

### Milestone B — export contract

- Define and test snapshot schema version 1.
- Add a narrowly scoped exporter to Arctra or a standalone adaptor around its
  public C++ interfaces.
- Generate a snapshot from a known CPF fixture.
- Preserve exact provider/file and coverage metadata.
- Add contract compatibility and coverage-boundary tests.

### Milestone C — observer and visibility UX

- Present current azimuth, elevation, range, visibility, and CPF health.
- Add separate ground-track and spatial-trajectory controls.
- Add the polar sky view and clearly labelled explanatory overlays.
- Add a temporary clicked observer with a non-survey-grade warning.

### Milestone D — live local updates

- Add the local backend service and chosen transport.
- Update valid state approximately once per second without blocking rendering.
- Reuse Arctra's acquisition, serialized refresh, expiry, validation, and
  no-extrapolation policies.
- Preserve usable loaded data during refresh failures without using it after
  expiry.

### Milestone E — packaging and delivery

- Add Windows build and test automation.
- Package the application with MSIX.
- Verify WebView2 runtime and Cesium asset deployment.
- Test installation, upgrade, offline behavior, UTF-8 text, high-DPI display,
  and graphics fallback on representative Windows systems.

## Likely Windows development prerequisites

The exact toolchain should be confirmed when the first project files are
generated. The likely baseline is:

- Windows 11;
- Visual Studio 2022 with Desktop development workloads;
- Windows App SDK / WinUI 3;
- WebView2 SDK and runtime;
- a current Node.js LTS release for the TypeScript globe;
- CMake, Ninja, and an MSVC C++20 compiler for the backend;
- libcurl, probably managed with vcpkg if the backend builds Arctra sources;
- CesiumJS and a modern TypeScript build/test toolchain.

Do not add dependencies until their role and packaging implications are clear.
Pin versions through the appropriate lock files once selected.

## Testing priorities

- Validate JSON instances against the published schema.
- Test units, required provenance, coverage boundaries, expiry, and unsupported
  states at the contract boundary.
- Test globe state management independently from rendering.
- Test the C++ adaptor against Arctra fixtures and known tabulated epochs.
- Test that no trajectory or event is emitted outside usable CPF coverage.
- Keep above-threshold visibility distinct from CPF health in every layer.
- Treat screenshots and visual placement as UI tests, not independent orbital
  validation.
- Retain Arctra's exact-epoch operational comparison as a separate validation
  requirement.

## Open decisions

Before substantial implementation, decide and document:

1. Exact .NET, Windows App SDK, and development toolchain versions for the
   accepted C# / WinUI 3 shell.
2. Initial local-service transport: named pipes, HTTP/WebSocket, or another
   narrowly scoped local mechanism.
3. Whether Arctra is consumed as a built library, selected shared sources, or
   a separately versioned backend executable.
4. How Cesium imagery/terrain dependencies and any required access tokens are
   handled for development, packaging, and offline use. Never commit tokens.
5. Where the authoritative snapshot schema is versioned and how compatibility
   is negotiated.
6. Whether the first distributable targets only x64 or also ARM64.

## Immediate next steps

1. Verify and install development prerequisites for the accepted stack, then
   record the backend transport decision before live integration.
2. Define `prediction-snapshot-v1.schema.json` before building UI-specific
   model classes.
3. Prepare one small, reproducible sample snapshot from checked-in Arctra CPF
   fixture data, clearly labelled as test data.
4. Generate the WinUI 3 shell and CesiumJS projects in their existing source
   directories.
5. Render the offline snapshot before implementing live communication.

Avoid beginning with packaging, a DLL ABI, provider downloading in the UI, or
advanced visibility overlays. The smallest valuable proof is a native Windows
window containing a controllable globe that faithfully renders a versioned,
coverage-bounded Arctra snapshot.

## Observer profiles and minimum elevation (2026-11-16)

Implemented one Arctra-compatible [observatory] INI per station in LocalAppData/ArctraWin/Observers, with native station selector, New/Edit/Import/Save as/Reload/Open folder controls. Active filename persists in observer-profile.json; existing observer-v1.json coordinates migrate without changing their previous zero-degree cutoff. Fresh nyal.ini uses the approximate Arctra example and 12.5-degree minimum. Display preferences remain global.

Horizontal plane stays at zero degrees. A separate translucent ENU cone displays the minimum elevation and extends beyond selected geometry. C++ live frames now identify the observer/minimum and supply inclusive aboveThreshold alongside unchanged aboveHorizon. Host and renderer discard prior-station frames. Green current markers meet the limit; others are muted. Station switches retain the camera.

Validation: native Debug build has zero warnings/errors; pure C# profile/migration checks pass; CTest passes including equality boundary; real CPF integration passes observer and threshold changes; eleven Playwright tests pass. Native smoke verified 6/6 live CPF targets and advancing UTC after profile migration. See docs/observer-plane.md for format and workflow.

## Satellite marker styling (2026-11-16)

Globe markers now use a locally drawn two-panel satellite billboard and a world-space motion arrow. Live direction comes from consecutive backend-supplied ECEF positions within 3.5 seconds, never the approximate reference orbit. Recorded preview arrows use consecutive supplied CPF samples. Icons retain threshold colours and fixed pixel size; arrow length follows camera distance for readability and is not a speed scale. No physical spacecraft attitude is implied. Labels moved outward. Added a movement/reversal/stationary direction test.
