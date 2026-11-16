# Application stack

Date: 2026-11-16
Status: Accepted

## Decision

- Use C# and WinUI 3 for the native Windows application shell.
- Host a CesiumJS globe in WebView2, using TypeScript compiled to JavaScript
  for rendering and interaction.
- Keep CPF parsing, validation, interpolation, geometry, pass prediction,
  and coverage enforcement in the authoritative Arctra C++ backend.
- Exchange prediction data through an explicit, versioned JSON contract,
  beginning with an offline sample snapshot.

## Rationale and consequences

C# provides a straightforward route to native Windows controls and WebView2
hosting. CesiumJS supplies globe rendering and camera interaction, while the
existing C++ engine retains ownership of prediction mathematics.

The project therefore uses .NET, a Node.js/TypeScript build toolchain, and a
C++ toolchain when backend integration begins. Java is not required.

Neither C# nor TypeScript may reproduce orbital calculations or extrapolate
beyond valid CPF coverage. Geometric visibility and prediction validity remain
separate states.

Backend transport, Arctra integration method, dependency versions, imagery
deployment, and supported processor architectures remain open decisions.
