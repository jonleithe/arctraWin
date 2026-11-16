#include "snapshot.h"
#include <iostream>
#include <stdexcept>
#include <cmath>

void require(bool condition, const char* message) { if (!condition) throw std::runtime_error(message); }
template<class Action> void rejects(Action action) {
    bool rejected = false;
    try { action(); } catch (const std::exception&) { rejected = true; }
    require(rejected, "Invalid trajectory request was accepted");
}
int main(int argc, char** argv) {
    try {
        require(argc == 2, "Fixture required");
        const auto ephemeris = arctra::load_cpf(argv[1]);
        const auto start = arctrawin::parse_utc("2026-11-10T00:00:00Z");
        const auto snapshot = arctrawin::sample_trajectory(ephemeris, "lageos1", 8820, 7603901, start, 540, 30);
        require(snapshot.samples.size() == 19, "Expected inclusive endpoints");
        require(snapshot.samples.front().position.x_m == -9249789.876, "Tabulated CPF point changed");
        require(snapshot.samples[2].position.x_m == -9482919.321, "Exact epoch must match original CPF");
        require(snapshot.samples.back().epoch == start + std::chrono::seconds{540}, "End epoch changed");
        require(snapshot.coverage_end == snapshot.samples.back().epoch, "Coverage must use final record, not only H2 intent");
        const auto& loop = snapshot.reference_orbit.positions;
        require(loop.size() == 361 && loop.front().x_m == loop.back().x_m && loop.front().y_m == loop.back().y_m && loop.front().z_m == loop.back().z_m, "Reference loop must close exactly");
        require(loop.front().x_m == snapshot.samples.front().position.x_m, "Reference loop must pass through the CPF preview marker");
        // Analytic circular and eccentric states independently specify their
        // expected geometry, so these test the conic calculation, not a replay.
        constexpr double mu = 3.986004418e14;
        const auto circle = arctrawin::reference_from_state(start, {12000000,0,0}, {0,std::sqrt(mu/12000000),0});
        require(std::abs(circle.eccentricity) < 1e-12 && std::abs(circle.semi_major_axis_m-12000000) < 1e-6, "Analytic circular state was not preserved");
        for (const auto& point : circle.positions) require(std::abs(std::hypot(point.x_m,point.y_m)-12000000)<1e-6 && point.z_m==0, "Circle plane or radius incorrect");
        const auto ellipse = arctrawin::reference_from_state(start, {10800000,0,0}, {0,std::sqrt(mu*1.1/10800000),0});
        require(std::abs(ellipse.eccentricity-0.1)<1e-12 && std::abs(ellipse.positions[180].x_m+13200000)<1e-6, "Analytic ellipse apogee incorrect");
        rejects([&] { arctrawin::reference_from_state(start, {12000000,0,0}, {0,20000,0}); });
        rejects([&] { arctrawin::reference_from_state(start, {12000000,0,0}, {0,1000,0}); });
        rejects([&] { arctrawin::sample_trajectory(ephemeris, "lageos2", 22195, 9207002, start, 540, 30); });
        rejects([&] { arctrawin::sample_trajectory(ephemeris, "lageos1", 8820, 7603901, start, 600, 30); });
        rejects([&] { arctrawin::sample_trajectory(ephemeris, "lageos1", 8820, 7603901, start - std::chrono::seconds{30}, 540, 30); });
        rejects([&] { arctrawin::sample_trajectory(ephemeris, "lageos1", 8820, 7603901, start, 540, 0); });
        auto gap_records = ephemeris.positions();
        gap_records.erase(gap_records.begin() + 2, gap_records.begin() + 5);
        const arctra::CpfEphemeris gap_ephemeris{ephemeris.header(), gap_records};
        rejects([&] { arctrawin::sample_trajectory(gap_ephemeris, "lageos1", 8820, 7603901, start, 540, 60); });
        auto shorter_header = ephemeris.header();
        shorter_header.intended_end = start + std::chrono::seconds{480};
        const arctra::CpfEphemeris shorter{shorter_header, ephemeris.positions()};
        rejects([&] { arctrawin::sample_trajectory(shorter, "lageos1", 8820, 7603901, start, 540, 30); });
        rejects([] { arctrawin::parse_utc("2026-11-16T23:59:60Z"); });
        std::cout << "Snapshot boundaries, identity, gaps and original CPF epochs passed\n";
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
