#include "snapshot.h"
#include <algorithm>
#include <cctype>
#include <cmath>
#include <iomanip>
#include <sstream>
#include <stdexcept>
#include <numbers>

namespace arctrawin {
namespace {
using Vector = arctra::EarthFixedPosition;
Vector add(Vector a, Vector b) { return {a.x_m + b.x_m, a.y_m + b.y_m, a.z_m + b.z_m}; }
Vector scale(Vector a, double value) { return {a.x_m * value, a.y_m * value, a.z_m * value}; }
double dot(Vector a, Vector b) { return a.x_m*b.x_m + a.y_m*b.y_m + a.z_m*b.z_m; }
double length(Vector a) { return std::sqrt(dot(a, a)); }
Vector cross(Vector a, Vector b) { return {a.y_m*b.z_m-a.z_m*b.y_m, a.z_m*b.x_m-a.x_m*b.z_m, a.x_m*b.y_m-a.y_m*b.x_m}; }
// WGS 84 defining constants, NGA: https://earth-info.nga.mil/?action=wgs84&dir=wgs84
constexpr double earth_mu = 3.986004418e14; // m^3 / s^2
constexpr double earth_omega = 7.292115e-5; // rad / s, nominal mean rotation

std::string json_string(const std::string& value) {
    std::ostringstream result;
    result << '"';
    for (unsigned char c : value) {
        if (c == '"' || c == '\\') result << '\\' << c;
        else if (c < 32) result << "\\u" << std::hex << std::setw(4) << std::setfill('0') << static_cast<int>(c) << std::dec;
        else result << c;
    }
    result << '"';
    return result.str();
}
}

ReferenceOrbit reference_from_state(arctra::UtcTime epoch, Vector position, Vector velocity) {
    const auto radius = length(position);
    const auto angular_momentum = cross(position, velocity);
    const auto h = length(angular_momentum);
    const auto energy = dot(velocity, velocity) / 2 - earth_mu / radius;
    if (!std::isfinite(radius) || !std::isfinite(h) || radius < 6378137 || h < 1 || !(energy < 0))
        throw std::invalid_argument("Cannot construct a bound reference orbit from this state");
    const auto radial = scale(position, 1 / radius);
    const auto normal = scale(angular_momentum, 1 / h);
    const auto transverse = cross(normal, radial);
    const auto eccentricity_vector = add(scale(cross(velocity, angular_momentum), 1 / earth_mu), scale(radial, -1));
    const auto eccentricity = length(eccentricity_vector);
    const auto a = -earth_mu / (2 * energy);
    const auto p = h * h / earth_mu;
    if (!std::isfinite(a) || !std::isfinite(eccentricity) || eccentricity >= 1 || a*(1-eccentricity) < 6378137)
        throw std::invalid_argument("Reference ellipse is unbound or intersects Earth");
    ReferenceOrbit orbit{epoch, a, eccentricity, 2 * std::numbers::pi * std::sqrt(a*a*a/earth_mu), {}};
    // Start exactly at the CPF marker. Sweep geometry once around the two-body
    // conic, then repeat that first point exactly: no artificial chord closure.
    orbit.positions.push_back(position);
    for (int index = 1; index < 360; ++index) {
        const auto angle = 2 * std::numbers::pi * index / 360;
        const auto direction = add(scale(radial, std::cos(angle)), scale(transverse, std::sin(angle)));
        orbit.positions.push_back(scale(direction, p / (1 + dot(eccentricity_vector, direction))));
    }
    orbit.positions.push_back(position);
    return orbit;
}

arctra::UtcTime parse_utc(const std::string& value) {
    std::istringstream input{value};
    int year{}, month{}, day{}, hour{}, minute{}, second{};
    char a{}, b{}, t{}, c{}, d{}, z{};
    if (!(input >> year >> a >> month >> b >> day >> t >> hour >> c >> minute >> d >> second >> z) ||
        a != '-' || b != '-' || t != 'T' || c != ':' || d != ':' || z != 'Z' || input.peek() != EOF)
        throw std::invalid_argument("Expected a UTC timestamp YYYY-MM-DDTHH:MM:SSZ");
    const std::chrono::year_month_day date{std::chrono::year{year}, std::chrono::month{static_cast<unsigned>(month)}, std::chrono::day{static_cast<unsigned>(day)}};
    if (!date.ok() || hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
        throw std::invalid_argument("Invalid UTC timestamp (distinct leap-second labels are unsupported)");
    return std::chrono::sys_days{date} + std::chrono::hours{hour} + std::chrono::minutes{minute} + std::chrono::seconds{second};
}

std::string utc_string(arctra::UtcTime value) {
    const auto days = std::chrono::floor<std::chrono::days>(value);
    const std::chrono::year_month_day date{days};
    const std::chrono::hh_mm_ss time{std::chrono::floor<std::chrono::seconds>(value - days)};
    std::ostringstream output;
    output << std::setfill('0') << std::setw(4) << static_cast<int>(date.year()) << '-'
        << std::setw(2) << static_cast<unsigned>(date.month()) << '-' << std::setw(2) << static_cast<unsigned>(date.day())
        << 'T' << std::setw(2) << time.hours().count() << ':' << std::setw(2) << time.minutes().count()
        << ':' << std::setw(2) << time.seconds().count() << 'Z';
    return output.str();
}

TrajectorySnapshot sample_trajectory(const arctra::CpfEphemeris& ephemeris,
    const std::string& key, int norad_id, int ilrs_id, arctra::UtcTime start,
    int duration_seconds, int step_seconds) {
    const auto& header = ephemeris.header();
    std::string target_key;
    for (unsigned char c : header.target_name)
        if (c != '-' && c != '_' && !std::isspace(c)) target_key += static_cast<char>(std::tolower(c));
    if (header.format_version != 2 || header.reference_frame != 0 || target_key != key ||
        header.norad_catalog_id != norad_id || header.ilrs_satellite_id != ilrs_id)
        throw std::invalid_argument("CPF target identity or reference frame does not match the requested target");
    if (duration_seconds <= 0 || duration_seconds > 86400 || step_seconds <= 0 || step_seconds > 60 || duration_seconds % step_seconds != 0)
        throw std::invalid_argument("Require a duration up to 24h divisible by a sample step of 1–60 seconds");
    const auto coverage_start = std::max(header.intended_start, ephemeris.positions().front().epoch);
    const auto coverage_end = std::min(header.intended_end, ephemeris.positions().back().epoch);
    const auto end = start + std::chrono::seconds{duration_seconds};
    if (start < coverage_start || end > coverage_end)
        throw std::out_of_range("Requested trajectory is outside usable CPF coverage; extrapolation is forbidden");
    for (const auto& record : ephemeris.positions())
        if (record.leap_second_flag != 0) throw std::invalid_argument("CPF leap-second flags are unsupported for this preview");
    // Check original spacing as well: exact-epoch interpolation bypasses Arctra's
    // stencil-gap check, but the renderer must not connect a line across a gap.
    const auto maximum_gap = std::chrono::seconds{header.nominal_step_seconds > 0 ? header.nominal_step_seconds * 2 : 120};
    const auto& records = ephemeris.positions();
    for (std::size_t i = 1; i < records.size(); ++i)
        if (records[i].epoch > start && records[i - 1].epoch < end && records[i].epoch - records[i - 1].epoch > maximum_gap)
            throw std::runtime_error("Requested trajectory crosses a CPF data gap");
    if (duration_seconds < 4) throw std::invalid_argument("At least four seconds of CPF coverage are needed for a reference state");
    // Fourth-order, five-point finite difference of Arctra's CPF interpolation.
    // Use a central stencil where possible and a forward stencil at a boundary.
    Vector derivative{};
    if (start - std::chrono::seconds{2} >= coverage_start) {
        const int offsets[] = {-2, -1, 1, 2};
        const double weights[] = {1.0/12, -2.0/3, 2.0/3, -1.0/12};
        for (int i = 0; i < 4; ++i) derivative = add(derivative, scale(ephemeris.interpolate(start + std::chrono::seconds{offsets[i]}), weights[i]));
    } else {
        const double weights[] = {-25.0/12, 4, -3, 4.0/3, -1.0/4};
        for (int i = 0; i < 5; ++i) derivative = add(derivative, scale(ephemeris.interpolate(start + std::chrono::seconds{i}), weights[i]));
    }
    const auto reference_position = ephemeris.interpolate(start);
    // v_inertial = d(r_ECEF)/dt + omega x r, expressed in epoch-frozen axes.
    // Nominal z-axis rotation omits EOP/polar motion; this is illustrative only.
    const auto velocity = add(derivative, cross({0, 0, earth_omega}, reference_position));
    TrajectorySnapshot snapshot{header, {}, coverage_start, coverage_end, step_seconds, reference_from_state(start, reference_position, velocity)};
    for (auto epoch = start; epoch <= end; epoch += std::chrono::seconds{step_seconds}) {
        const auto position = ephemeris.interpolate(epoch);
        if (!std::isfinite(position.x_m) || !std::isfinite(position.y_m) || !std::isfinite(position.z_m))
            throw std::runtime_error("CPF produced a nonfinite position");
        snapshot.samples.push_back({epoch, 0, position});
    }
    return snapshot;
}

std::string snapshot_json(const TrajectorySnapshot& snapshot, const std::string& key,
    const std::string& display_name, const std::string& filename,
    const std::string& source_url, const std::string& retrieved_at) {
    const auto& h = snapshot.header;
    const auto now = std::chrono::time_point_cast<std::chrono::milliseconds>(std::chrono::system_clock::now());
    std::ostringstream output;
    output << std::setprecision(15)
        << "{\n  \"schemaVersion\": 1,\n  \"predictionKind\": \"cpf-ecef-geometric\",\n  \"displayMode\": \"recorded-preview\",\n"
        << "  \"generatedAtUtc\": " << json_string(utc_string(now)) << ",\n"
        << "  \"epochUtc\": " << json_string(utc_string(snapshot.samples.front().epoch)) << ",\n"
        << "  \"target\": {\"key\": " << json_string(key) << ", \"name\": " << json_string(display_name)
        << ", \"noradId\": " << h.norad_catalog_id << ", \"ilrsId\": " << h.ilrs_satellite_id << "},\n"
        << "  \"cpf\": {\"provider\": " << json_string(h.ephemeris_source) << ", \"filename\": " << json_string(filename)
        << ", \"sourceUrl\": " << json_string(source_url) << ", \"retrievedAtUtc\": " << json_string(retrieved_at)
        << ", \"referenceFrame\": 0, \"usableStartUtc\": " << json_string(utc_string(snapshot.coverage_start))
        << ", \"usableEndUtc\": " << json_string(utc_string(snapshot.coverage_end)) << "},\n"
        << "  \"trajectory\": {\"sampleStepSeconds\": " << snapshot.sample_step_seconds << ", \"samples\": [\n";
    bool first = true;
    for (const auto& sample : snapshot.samples) {
        if (!first) output << ",\n";
        first = false;
        output << "    {\"epochUtc\": " << json_string(utc_string(sample.epoch)) << ", \"positionEcefM\": ["
            << sample.position.x_m << ',' << sample.position.y_m << ',' << sample.position.z_m << "]}";
    }
    const auto& orbit = snapshot.reference_orbit;
    output << "\n  ]},\n  \"referenceOrbit\": {\"model\": \"approximate-two-body\", \"frame\": \"epoch-frozen-terrestrial\","
        << " \"epochUtc\": " << json_string(utc_string(orbit.epoch))
        << ", \"semiMajorAxisM\": " << orbit.semi_major_axis_m << ", \"eccentricity\": " << orbit.eccentricity
        << ", \"periodSeconds\": " << orbit.period_seconds << ", \"positionsM\": [\n";
    first = true;
    for (const auto& point : orbit.positions) {
        if (!first) output << ",\n";
        first = false;
        output << "    [" << point.x_m << ',' << point.y_m << ',' << point.z_m << ']';
    }
    output << "\n  ]}\n}\n";
    return output.str();
}
}
