#pragma once
#include "arctra/cpf.h"
#include <string>

namespace arctrawin {
struct ReferenceOrbit {
    arctra::UtcTime epoch;
    double semi_major_axis_m;
    double eccentricity;
    double period_seconds;
    std::vector<arctra::EarthFixedPosition> positions;
};
// Approximate two-body geometry, expressed in the terrestrial axes frozen at
// the reference epoch. It is not a time-tagged CPF prediction or propagation.
// The velocity argument is Cartesian metres/second in those same axes.
ReferenceOrbit reference_from_state(arctra::UtcTime epoch,
    arctra::EarthFixedPosition position, arctra::EarthFixedPosition inertial_velocity);
struct TrajectorySnapshot {
    arctra::CpfHeader header;
    std::vector<arctra::CpfPositionRecord> samples;
    arctra::UtcTime coverage_start;
    arctra::UtcTime coverage_end;
    int sample_step_seconds;
    ReferenceOrbit reference_orbit;
};
arctra::UtcTime parse_utc(const std::string& value);
std::string utc_string(arctra::UtcTime value);
TrajectorySnapshot sample_trajectory(const arctra::CpfEphemeris& ephemeris,
    const std::string& key, int norad_id, int ilrs_id,
    arctra::UtcTime start, int duration_seconds, int step_seconds);
std::string snapshot_json(const TrajectorySnapshot& snapshot, const std::string& key,
    const std::string& display_name, const std::string& filename,
    const std::string& source_url, const std::string& retrieved_at);
}
