#include "live_worker.h"
#include <iostream>

int main() {
    arctrawin::LiveWorker worker;
    std::string line;
    while (std::getline(std::cin,line)) {
        try { std::cout << worker.handle(nlohmann::json::parse(line)).dump() << '\n' << std::flush; }
        catch (const std::exception& error) {
            std::cout << nlohmann::json{{"schemaVersion",1},{"ok",false},{"error",error.what()}}.dump() << '\n' << std::flush;
        }
    }
}
