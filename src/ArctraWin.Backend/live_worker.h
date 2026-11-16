#pragma once
#include "snapshot.h"
#include "nlohmann/json.hpp"
#include <map>

namespace arctrawin {
class LiveWorker {
    struct Cached { std::filesystem::file_time_type modified; arctra::CpfEphemeris ephemeris; };
    std::map<std::string, Cached> cache_;
    const arctra::CpfEphemeris& load(const nlohmann::json& target);
public:
    nlohmann::json handle(const nlohmann::json& request);
};
}
