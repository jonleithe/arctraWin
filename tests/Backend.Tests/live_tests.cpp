#include "live_worker.h"
#include <iostream>

void require(bool condition,const char* message) { if (!condition) throw std::runtime_error(message); }
int main(int argc,char** argv) {
    try {
        require(argc==2,"Fixture required");
        arctrawin::LiveWorker worker;
        using Json=nlohmann::json;
        Json target{{"key","lageos1"},{"name","LAGEOS-1"},{"noradId",8820},{"ilrsId",7603901},{"path",argv[1]},
            {"sourceUrl","https://example.test/cpf"},{"retrievedAtUtc","2026-11-10T00:00:00Z"}};
        Json request{{"schemaVersion",1},{"operation","evaluate"},{"epochUtc","2026-11-10T00:00:00Z"},
            {"observer",{{"latitudeDeg",0},{"longitudeDeg",0},{"ellipsoidalHeightM",0}}},{"targets",Json::array({target})},{"includeGeometry",true}};
        auto frame=worker.handle(request);
        require(frame["targets"][0]["state"]=="ready","Current fixture epoch must be ready");
        require(frame["targets"][0]["positionEcefM"][0]==-9249789.876,"Tabulated live position must equal CPF");
        require(frame["snapshots"].size()==1 && frame["snapshots"][0]["displayMode"]=="live","Live geometry required");
        const auto first=frame["targets"][0]["positionEcefM"];
        const auto elevation=frame["targets"][0]["look"]["elevationDeg"].get<double>();
        request["observer"]["minElevationDeg"]=elevation;
        require(worker.handle(request)["targets"][0]["look"]["aboveThreshold"]==true,"Equality at the elevation limit must count as above threshold");
        request["observer"]["minElevationDeg"]=elevation+0.1;
        require(worker.handle(request)["targets"][0]["look"]["aboveThreshold"]==false,"Raising the limit must change visibility independently of position");
        request["observer"]["minElevationDeg"]=0;
        request["epochUtc"]="2026-11-10T00:00:01Z";
        require(worker.handle(request)["targets"][0]["positionEcefM"]!=first,"Position must advance at the next second");
        request["observer"]["latitudeDeg"]=80;
        require(worker.handle(request)["targets"][0]["look"]!=frame["targets"][0]["look"],"Observer changes must recompute look geometry");
        request["epochUtc"]="2026-11-10T00:09:01Z";
        frame=worker.handle(request);
        require(frame["targets"][0]["state"]=="expired" && !frame["targets"][0].contains("positionEcefM"),"Never emit a position beyond last record");
        request["epochUtc"]="2026-11-09T23:59:59Z";
        require(worker.handle(request)["targets"][0]["state"]=="outside-coverage","Reject before first sample");
        request["epochUtc"]="2026-11-10T00:00:00Z";
        request["targets"][0]["noradId"]=22195;
        require(worker.handle(request)["targets"][0]["state"]=="error","Reject mismatched target identity");
        request["targets"]=Json::array();
        require(worker.handle(request)["targets"].empty(),"Deselection must remove target states");
        std::cout << "Live cadence, observer, identity and coverage checks passed\n";
    } catch(const std::exception& error) { std::cerr<<error.what()<<'\n'; return 1; }
}
