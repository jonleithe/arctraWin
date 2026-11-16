# Tests

- `App.Tests` — view models and native application behavior.
- `Backend.Tests` — C++ service/adaptor and Arctra integration.
- `Globe.Tests` — TypeScript state handling and rendering-independent logic.
- `Contract.Tests` — schema validation and compatibility fixtures.

Rendering tests must not be treated as independent validation of the orbital
or topocentric mathematics.

Run catalog and selection checks with:

```powershell
dotnet run --project tests/App.Tests/ArctraWin.Catalog.Tests.csproj
```

This dependency-free executable tests the actual bundled catalog and the native
shell's catalog/search/selection model, including leading-zero IDs, lunar
exclusion, retained selections during filtering, saved-selection round trips,
unknown keys, and invalid inputs.

`scripts/build-backend.ps1` builds and runs the C++ exporter checks with CTest.
They cover original tabulated CPF epochs, target mismatch, both record and
header coverage boundaries, gaps, and unsupported leap-second labels.

After building the globe, `npm.cmd test` in `src/ArctraWin.Globe` validates the
real exported fixture, rejects invalid contracts and coverage, renders the
real 3D trajectory in Edge, tests show/hide/removal, and checks that no external
requests are needed. Screenshots are saved under ignored `test-results/`.

Closed reference-loop checks also cover analytic circular/eccentric states,
bound/Earth-intersecting rejection, exact endpoint closure, agreement with the
CPF marker, and multi-target selection/removal. Reference loops and open CPF
trails have independent display controls. Four Playwright tests cover the
bundled LAGEOS-1, LAGEOS-2, LARES and LARES-2 previews.

The same C# check executable tests observer input ranges, poles, saved-coordinate
round trips, plane visibility persistence and unsupported settings formats.
The globe suite also checks WGS 84 rendering frames at poles/equator and across
hemispheres, height-independent geodetic Up, relocation, invalid-message
rejection, plane visibility, side framing and free camera rotation. The complete
Playwright suite now contains eleven tests, including live-frame schema/freshness
checks, advancing one-second rendering and interruption handling.

The C++ live tests exercise consecutive-second interpolation, observer changes,
wrong identities, pre-coverage and post-final-record rejection, and deselection.
The C# checks also verify EDC filename/provider ordering and refresh cadence.
For an actual EDC/cache/worker integration check, run:

```powershell
dotnet run --project tests/App.Tests/ArctraWin.Catalog.Tests.csproj -- --live-integration build/backend/Debug/arctrawin-live.exe build/live-test-cache
```

That opt-in network check requires real current CPF availability and writes
successful frames to ignored `build/live-test-cache/live-proof.json`.

An additional plane-bounds test covers distant selected geometry beyond the old
40000 km limit, margin containment, shrinkage on removal, fallback size and
observer relocation. Cone tests cover configured angles, closure and limiting
angles; rendering checks toggle the cone independently of the horizontal plane.
Profile checks cover INI parsing, migration, imports/copy naming, active-file
persistence and invalid-file handling. Backend tests cover inclusive elevation
thresholds; the live integration check also changes the minimum elevation.
