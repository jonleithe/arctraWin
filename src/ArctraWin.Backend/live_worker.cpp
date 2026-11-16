#include "live_worker.h"
#include "arctra/coordinates.h"
#include <algorithm>
#include <cctype>
#include <cmath>

namespace arctrawin {
using Json = nlohmann::json;
namespace {
std::pair<arctra::UtcTime, arctra::UtcTime> coverage(const arctra::CpfEphemeris& e) {
    return {std::max(e.header().intended_start, e.positions().front().epoch),
        std::min(e.header().intended_end, e.positions().back().epoch)};
}
Json metadata(const arctra::CpfEphemeris& e, const Json& target) {
    const auto [start,end] = coverage(e);
    return { {"provider",e.header().ephemeris_source}, {"filename",std::filesystem::u8path(target.at("path").get<std::string>()).filename().string()},
        {"usableStartUtc",utc_string(start)}, {"usableEndUtc",utc_string(end)},
        {"sourceUrl",target.value("sourceUrl",std::string{})}, {"retrievedAtUtc",target.value("retrievedAtUtc",std::string{})}, {"referenceFrame",0} };
}
}
const arctra::CpfEphemeris& LiveWorker::load(const Json& target) {
    const auto path = target.at("path").get<std::string>();
    const auto native = std::filesystem::u8path(path);
    const auto modified = std::filesystem::last_write_time(native);
    auto found = cache_.find(path);
    if (found == cache_.end() || found->second.modified != modified) {
        auto ephemeris = arctra::load_cpf(native);
        for (const auto& record : ephemeris.positions()) {
            if (record.leap_second_flag != 0) throw std::runtime_error("CPF leap-second flags are unsupported");
            if (!std::isfinite(record.position.x_m) || !std::isfinite(record.position.y_m) || !std::isfinite(record.position.z_m))
                throw std::runtime_error("CPF contains a nonfinite position");
        }
        if (cache_.size() > 300) cache_.clear();
        found = cache_.insert_or_assign(path, Cached{modified, std::move(ephemeris)}).first;
    }
    const auto& ephemeris = found->second.ephemeris;
    const auto& header = ephemeris.header();
    std::string key;
    for (unsigned char c : header.target_name)
        if (c!='-' && c!='_' && !std::isspace(c)) key += static_cast<char>(std::tolower(c));
    if (key != target.at("key").get<std::string>() || header.norad_catalog_id != target.at("noradId").get<int>() ||
        header.ilrs_satellite_id != target.at("ilrsId").get<int>() || header.reference_frame != 0)
        throw std::runtime_error("CPF header does not match the requested target");
    return ephemeris;
}

Json LiveWorker::handle(const Json& request) {
    if (request.at("schemaVersion") != 1) throw std::runtime_error("Unsupported live request schema");
    const auto epoch = parse_utc(request.at("epochUtc").get<std::string>());
    if (request.at("operation") == "validate") {
        const auto& target = request.at("target");
        const auto& ephemeris = load(target);
        (void)ephemeris.interpolate(epoch); // validates intended coverage, record coverage and interpolation gaps
        return {{"schemaVersion",1},{"ok",true},{"cpf",metadata(ephemeris,target)}};
    }
    if (request.at("operation") != "evaluate") throw std::runtime_error("Unknown live operation");
    const auto& observer = request.at("observer");
    const arctra::GeodeticPosition site{observer.at("latitudeDeg"),observer.at("longitudeDeg"),observer.at("ellipsoidalHeightM")};
    const double minimum_elevation = observer.value("minElevationDeg", 0.0);
    if (!std::isfinite(site.latitude_deg) || std::abs(site.latitude_deg)>90 || !std::isfinite(site.longitude_deg) ||
        std::abs(site.longitude_deg)>180 || !std::isfinite(site.ellipsoidal_height_m) || !std::isfinite(minimum_elevation) || std::abs(minimum_elevation)>90)
        throw std::runtime_error("Invalid observer coordinates or minimum elevation");
    Json result{{"schemaVersion",1},{"ok",true},{"epochUtc",utc_string(epoch)}, {"targets",Json::array()}};
    result["observer"] = {{"latitudeDeg",site.latitude_deg},{"longitudeDeg",site.longitude_deg},{"ellipsoidalHeightM",site.ellipsoidal_height_m},{"minElevationDeg",minimum_elevation}};
    const auto geometry = request.value("includeGeometry",false);
    if (geometry) result["snapshots"] = Json::array();
    for (const auto& target : request.at("targets")) {
        Json state{{"key",target.at("key")},{"name",target.at("name")},{"state","loading"}};
        if (!target.contains("path") || target.at("path").get<std::string>().empty()) {
            state["state"] = target.value("state",std::string{"loading"});
            state["message"] = target.value("message",std::string{"Loading CPF"});
            result["targets"].push_back(state);
            continue;
        }
        try {
            const auto& ephemeris = load(target);
            const auto [start,end] = coverage(ephemeris);
            state["cpf"] = metadata(ephemeris,target);
            if (epoch < start || epoch > end) {
                state["state"] = epoch > end ? "expired" : "outside-coverage";
                state["message"] = "Current epoch is outside usable CPF coverage";
            } else {
                const auto position = ephemeris.interpolate(epoch);
                const auto look = arctra::topocentric_look_angle(site, position);
                if (!std::isfinite(position.x_m) || !std::isfinite(position.y_m) || !std::isfinite(position.z_m) ||
                    !std::isfinite(look.azimuth_deg) || !std::isfinite(look.elevation_deg) || !std::isfinite(look.slant_range_m))
                    throw std::runtime_error("Nonfinite CPF state");
                state["state"] = "ready";
                state["positionEcefM"] = {position.x_m,position.y_m,position.z_m};
                state["look"] = {{"azimuthDeg",look.azimuth_deg},{"elevationDeg",look.elevation_deg},{"slantRangeM",look.slant_range_m},{"aboveHorizon",look.elevation_deg>=0},{"aboveThreshold",look.elevation_deg>=minimum_elevation}};
                state["message"] = target.value("message",std::string{});
                if (geometry) {
                    try {
                        const auto remaining = std::chrono::duration_cast<std::chrono::seconds>(end-epoch).count();
                        const int duration = static_cast<int>(std::min<int64_t>(14400,remaining)/30*30);
                        if (duration < 30) throw std::runtime_error("Too little CPF coverage remains for a trail");
                        const auto sampled = sample_trajectory(ephemeris,target.at("key"),target.at("noradId"),target.at("ilrsId"),epoch,duration,30);
                        auto snapshot = Json::parse(snapshot_json(sampled,target.at("key"),target.at("name"),state["cpf"]["filename"],
                            target.value("sourceUrl",std::string{}),target.value("retrievedAtUtc",std::string{})));
                        snapshot["displayMode"] = "live";
                        result["snapshots"].push_back(snapshot);
                    } catch (const std::exception& error) { state["geometryError"] = error.what(); }
                }
            }
        } catch (const std::exception& error) { state["state"]="error"; state["message"]=error.what(); }
        result["targets"].push_back(state);
    }
    return result;
}
}
