# Data contracts

Store versioned schemas here before implementing transport. Contracts must use
explicit units and ISO-8601 UTC timestamps, identify CPF provider/file and
coverage, and distinguish prediction validity from geometric visibility.

Initial schema candidates:

- `schemas/prediction-snapshot-v1.schema.json`
- `schemas/backend-message-v1.schema.json`

Implemented: `schemas/trajectory-snapshot-v1.schema.json`, a deliberately
narrow single-target recorded preview contract. It supplies provenance, usable
CPF coverage, an explicit preview epoch, and timestamped Earth-fixed metre
samples. It does not yet claim observer geometry, live state, or pass events.
The globe validates both JSON Schema and temporal/coverage invariants before
rendering. Invalid replacements remove the prior trajectory.

The optional `referenceOrbit` extension supplies closed, approximate two-body
geometry in axes frozen at its reference epoch. It is distinct from time-tagged
CPF samples. Validators require exact closure and agreement with the CPF marker
and epoch. See the closed reference-orbit architecture decision for limits.

Implemented live contract: `schemas/live-state-v1.schema.json`. A frame has one
explicit current UTC epoch and one readiness/coverage state per selected target.
Ready targets have CPF provenance, ECEF metre positions and backend look angles;
expired/invalid targets have no position or look angles. Optional geometry uses
the trajectory schema with `displayMode: "live"`; recorded fixtures retain
`displayMode: "recorded-preview"`. The renderer enforces the schema and freshness
in addition to coverage and unit invariants.

Live frames identify the observer coordinates and `minElevationDeg` used for
calculation. Each ready look angle includes `aboveThreshold` (elevation greater
than or equal to that limit), while `aboveHorizon` retains its 0-degree meaning.
Both host and globe reject results for a previously selected observer/limit.
