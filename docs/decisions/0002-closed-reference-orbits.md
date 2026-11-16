# Closed reference orbits alongside CPF trails

Date: 2026-11-16
Status: Accepted

The user wants a complete orbit loop for selected satellites. A multi-hour
Earth-fixed CPF trajectory does not generally close, because its coordinates
rotate with Earth. Connecting its endpoints adds a segment that the CPF did not
predict. The application therefore exposes two separate visualizations:

- **Closed reference orbits**, enabled by default: approximate two-body conics
  derived at a shared recorded epoch. These are illustrative spatial geometry.
- **CPF trails**, disabled by default: time-tagged, coverage-bounded Arctra CPF
  samples. They remain open and may be toggled independently.

The C++ adaptor computes the reference geometry; neither C# nor TypeScript
implements orbital calculations. Arctra's upstream CPF parser/interpolator
remains unchanged. The adaptor differentiates its ECEF position using a
fourth-order five-point stencil at one-second spacing: central where coverage
permits, forward at a boundary. No derivative sample is outside CPF coverage.
Interpolation gap checks still apply.

The approximate inertial velocity, expressed in terrestrial axes frozen at
that epoch, is `v = dr_ECEF/dt + omega × r`. WGS 84 defining values are
`omega = 7.292115e-5 rad/s` and `mu = 3.986004418e14 m³/s²`, from
[NGA's WGS 84 defining parameters](https://earth-info.nga.mil/?action=wgs84&dir=wgs84).
This omits polar motion and time-varying Earth orientation and is not a precise
inertial-frame transformation.

For that state, the adaptor computes `h = r × v`,
`e = (v × h)/mu - r/|r|`, `p = |h|²/mu`, and
`a = -mu/(2*(|v|²/2 - mu/|r|))`. In the plane normal to `h`, it sweeps
`r(theta) = p*u(theta)/(1 + e·u(theta))` through one revolution. The first point
is the CPF marker, repeated exactly as the last point. This produces a closed
conic rather than a closing chord across the Earth-fixed trail.

Nonfinite, degenerate, unbound, or Earth-intersecting states are rejected. Tests
include analytic circular/eccentric states with independently specified expected
radii and apogee, exact loop closure, and agreement at the CPF marker.

`referenceOrbit` is an optional object in the snapshot contract. Its vertices
have no prediction timestamps. Its frame is `epoch-frozen-terrestrial`, its model
is `approximate-two-body`, and it retains its reference epoch. It must not be used
for pass finding, visibility, pointing, predicted positions, or extrapolation.

The shell sends selected snapshots with loaded data; missing targets are listed
as having no loaded CPF data. Initial bundled targets are LAGEOS-1, LAGEOS-2,
LARES and LARES-2 at 2026-11-16 09:00 UTC. The globe validates closure and
marker/epoch agreement, assigns distinct colours, and removes deselected
geometry. Both views remain recorded previews until live acquisition exists.

Subsequent implementation: ADR 0003 adds live acquisition. Current markers are
now updated each second, while reference loops/trails refresh each minute.
The geometric distinction and approximation limits above remain unchanged.
