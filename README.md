# ArctraWin

Windows desktop companion for Arctra. The application will combine a native
Windows shell with an embedded CesiumJS globe while keeping CPF parsing,
interpolation, pass prediction, and geometric calculations in the authoritative
Arctra C++ backend.

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

The first milestone is an offline visual proof using a checked-in, explicitly
labelled sample snapshot. Live local updates come only after the contract and
static view are tested.
