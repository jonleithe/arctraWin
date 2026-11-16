# Live backend transport and acquisition scheduling

Date: 2026-11-16
Status: Accepted

Use a private persistent C++ child worker with versioned UTF-8 JSON lines over
redirected stdin/stdout. The native C# app owns its lifecycle and serializes
request/response pairs. This avoids a listening port or shared ABI and keeps
Arctra's calculation code authoritative. Operations are `validate` and `evaluate`,
both requiring `schemaVersion: 1` and an explicit whole-second UTC epoch.

`validate` accepts a catalog identity and CPF path, checks the supported headers,
and evaluates the current epoch before admitting a cached/downloaded file.
`evaluate` accepts selected catalog identities/validated paths and the configured
observer. It returns `live-state-v1.schema.json`: a state for every selected
target and optional trajectory/reference snapshots. Invalid/expired targets
carry no position or look angles. One target's error does not abort the others.

The Windows host uses .NET HTTP for acquisition rather than introducing a
libcurl deployment dependency. Its queue follows the inspected Arctra acquisition
policy: issue ordering, same-issue provider preference, header validation before
promotion, usable-coverage enforcement, hourly/near-expiry checks, and five-minute
retries. This is a Windows transport adaptor; interpolation and geometric
calculations remain the unchanged pinned C++ sources.

One-second evaluations and serialized acquisition run independently. Network
requests never block rendering. Geometry updates are requested once per minute
or when selected CPF paths change; marker/look state is evaluated every second.
The renderer validates live-frame age and hides interrupted/expired state.
Recorded fixtures remain development/test artifacts and are not live fallbacks.

The worker uses the pinned MIT-licensed nlohmann/json 3.12.0 single header for
protocol parsing. It links the MSVC runtime statically so deployment needs only
the worker executable alongside the existing native application dependencies.
