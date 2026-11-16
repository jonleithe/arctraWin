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
- No application implementation has started yet.

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

The shell language has not been finalized. A C# WinUI 3 shell is likely the
most direct route, while C++/WinRT would keep more of the application in C++ at
the cost of additional UI complexity. This choice should be recorded as an
architecture decision before implementation.

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

- Finalize the shell language and record the decision.
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

1. C# WinUI 3 or C++/WinRT for the native shell.
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

1. Add architecture decision records for the shell language and backend
   transport.
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
