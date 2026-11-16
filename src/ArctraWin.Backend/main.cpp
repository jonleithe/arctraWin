#include "snapshot.h"
#include <fstream>
#include <iostream>
#include <map>

int main(int argc, char** argv) {
    try {
        std::map<std::string, std::string> args;
        for (int i = 1; i < argc; i += 2) {
            if (i + 1 >= argc || !args.emplace(argv[i], argv[i + 1]).second)
                throw std::invalid_argument("Provide unique --option value pairs");
        }
        const auto get = [&](const std::string& key) -> const std::string& {
            const auto found = args.find(key);
            if (found == args.end()) throw std::invalid_argument("Missing option " + key);
            return found->second;
        };
        const auto ephemeris = arctra::load_cpf(get("--cpf"));
        const auto snapshot = arctrawin::sample_trajectory(ephemeris, get("--key"), std::stoi(get("--norad")),
            std::stoi(get("--ilrs")), arctrawin::parse_utc(get("--start")), std::stoi(get("--duration")), std::stoi(get("--step")));
        arctrawin::parse_utc(get("--retrieved-at"));
        const auto json = arctrawin::snapshot_json(snapshot, get("--key"), get("--name"),
            std::filesystem::path(get("--cpf")).filename().string(), get("--source-url"), get("--retrieved-at"));
        std::ofstream output{get("--output"), std::ios::binary};
        if (!output || !(output << json)) throw std::runtime_error("Cannot write snapshot output");
        std::cout << "Exported " << snapshot.samples.size() << " validated ECEF samples\n";
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
