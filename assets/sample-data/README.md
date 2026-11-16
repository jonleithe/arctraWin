# Recorded trajectory and reference-orbit previews

Each `*-trajectory-v1.json` contains 481 real CPF-based Earth-fixed positions
from **2026-11-16 09:00:00 UTC through 13:00:00 UTC**, sampled every 30 seconds.
It is a fixed recorded prediction preview, not current telemetry or a live
tracker. Its timestamps do not advance with the wall clock. Historical replay
within the recorded CPF coverage remains possible after that coverage expires;
the fixture must never be treated as a current prediction then.

The complete provider file is retained under `cpf/`:

- File: `lageos1_cpf_261115_31902.dgf`
- Source: <https://edc.dgfi.tum.de/pub/slr/cpf_predicts_v2/current/lageos1_cpf_261115_31902.dgf>
- Retrieved: 2026-11-16 09:02:29 UTC (also in `cpf/source.json`).
- Provider: DGF; CPF v2, passive Earth-orbiting target, common-epoch direction 0,
  geocentric true body-fixed reference-frame flag 0.
- Intended and sampled usable coverage: 2026-11-15 through 2026-11-22 UTC.
- Target: LAGEOS-1, NORAD 8820, ILRS 7603901.

Additional complete provider files and individual retrieval times are recorded
in `cpf/additional-sources.json`:

- LAGEOS-2: `lageos2_cpf_261115_32001.sgf`
- LARES: `lares_cpf_261115_32001.sgf`
- LARES-2: `lares2_cpf_261115_32001.sgf`

Those SGF files were also retrieved from EDC's current CPF directory on
2026-11-16. Each is parsed and checked against its own catalog IDs before export.

The C++ exporter uses the pinned Arctra parser and ten-point, ninth-order
Lagrange interpolation unchanged. Exact tabulated epochs are returned directly.
Only the exporter computes/interpolates predictions; C# selects data and
TypeScript renders it. The JSON schema is
`contracts/schemas/trajectory-snapshot-v1.schema.json`.

Regenerate from the bundled CPF without downloading anything:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/export-sample.ps1
```

This requires Visual Studio C++20 tools and CMake. It builds the exporter, runs
its boundary/identity/gap checks, and regenerates the fixture. `generatedAtUtc`
changes on regeneration; the prediction interval and positions remain fixed.
Run the normal GUI build afterwards to copy the updated snapshot.

The optional **CPF trail** joins supplied Cartesian metre coordinates in 3D.
It is not clamped to the ground, scaled to an invented altitude, or closed by
connecting its end to its start. An Earth-fixed multi-hour path need not form a
closed circle because Earth rotates during that interval. Camera framing uses
only the supplied points and does not change their coordinates.

Each snapshot also contains a **closed reference orbit**, an approximate
two-body ellipse calculated in C++ from the CPF state at 09:00 UTC. These loops
are enabled by default and separate from the original CPF trails. Vertices
are expressed in terrestrial axes frozen at the reference epoch and have no
prediction timestamps. The calculation uses nominal Earth rotation, omits EOP
and polar motion, and must not be used as an authoritative prediction.
See `docs/decisions/0002-closed-reference-orbits.md` for equations and limits.

The default overview uses an oblique camera angle, a quiet background, and
camera-relative illustrative lighting to make the globe's shape readable.
The shading does **not** indicate the physical Sun, day/night boundary, or
satellite illumination. Coloured markers show preview starts; when CPF trails
are enabled, smaller muted markers show their ends.

This is a rendering and integration proof, not independent validation of CPF
accuracy or operational pointing. No observer geometry, pass events, light-time
corrections, or telescope/laser control is provided by this snapshot contract.
Arctra's existing UTC/leap-second limitations still apply.

The small upstream CPF excerpt in `tests/Backend.Tests/fixtures/` is copied from
the same pinned Arctra commit as the parser and is used only for boundary tests.
