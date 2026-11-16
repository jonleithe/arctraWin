# Live CPF tracking

Tracking starts automatically when the globe is ready. Select any supported
Earth-orbiting catalog target: the app queues its current CPF acquisition from
EDC, validates the header and interpolation coverage, and displays current
predicted positions once per second. There is no fallback to recorded preview
JSON in live mode. A target without usable current data has no live marker.

The **LIVE CPF** badge counts ready/selected targets. The status bar shows the
actual UTC state epoch. Target rows show loading, CPF errors/expiry, or current
elevation above/below the configured station minimum. Hover a target for filename,
coverage, retrieval time, azimuth, elevation and range. Green markers mean
geometric elevation at least the configured minimum according to Arctra. Markers
below the limit are muted. This includes the station's elevation cutoff, but
does not include terrain, obstructions or other operational pointing constraints.

Positions and observer look angles update each second. Closed reference loops
and forward CPF trails refresh on a new selection/file and each UTC minute.
Loops remain approximate two-body geometry, not the source of live positions.
Trails are clipped to the earlier of four hours ahead and usable CPF coverage.
If geometry cannot be generated near a coverage boundary, a valid current
position may still be displayed without a trail or reference loop.

The native host runs a private `Backend/arctrawin-live.exe` child process. It
exchanges version-1 UTF-8 JSON lines over redirected stdin/stdout. The C++ worker
uses unchanged Arctra CPF parsing, interpolation, WGS 84 geodetic conversion,
and topocentric look-angle routines. It caches parsed files and checks identity
and coverage on every request. C# handles network acquisition, scheduling,
selection and observer settings; TypeScript validates and renders supplied state.

The acquisition queue is serialized independently from the one-second update
loop. It uses EDC's verified current directory:
<https://edc.dgfi.tum.de/pub/slr/cpf_predicts_v2/current/>.
Newest issue wins; DGF then HTS are preferred only for identical issue numbers,
matching Arctra's deterministic provider policy. Downloads are written to a
temporary file, parsed and validated in C++, and promoted only on success.

Validated CPF cache files and source/retrieval sidecars live under
`%LOCALAPPDATA%/ArctraWin/cpf/`. EDC is checked hourly, or thirty minutes before
usable expiry if sooner. Near expiry and after failure, checks retry after five
minutes. Valid loaded data survives a refresh failure but never remains usable
beyond the intersection of CPF intended and actual record coverage. Cached data
is also validated against the current epoch before use on restart.

The renderer checks JSON Schema, identity uniqueness, metres, coverage and frame
freshness. It hides geometry after a backend failure or roughly 3.5 seconds
without a fresh state, and hides targets at expiry. It never moves a marker by
client-side extrapolation. Late results for a previous selection/observer are
discarded. Closing the window cancels acquisition and stops the owned worker.

The live worker is included by the normal build script, which now requires
Visual Studio's C++20 tools and CMake as well as .NET/WinUI and Node. To run a
real-network integration check without opening the GUI:

```powershell
dotnet run --project tests/App.Tests/ArctraWin.Catalog.Tests.csproj -- --live-integration build/backend/Debug/arctrawin-live.exe build/live-test-cache
```

This checks real EDC acquisition, consecutive one-second states, observer and minimum-elevation changes,
partial deselection and clearing the selection. `build/live-test-cache/live-proof.json`
records the successful frames for inspection. Pure checks run without the flag;
the C++ CTest suite covers exact tabulated epochs and coverage/identity boundaries.

CPF predictions are not spacecraft telemetry. Arctra's supported passive,
Earth-orbiting, common-epoch frame-0 subset, UTC/leap-second limitations, and
pending independent operational accuracy validation still apply.
