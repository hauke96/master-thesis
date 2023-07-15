#!/bin/python

# Generates a CSV file containing the beeline distance, hiker trip distance and graph-based routed distance for a given dataset.
# Parameters are therefore:
#  1. hiker routing CSV file (e.g. "../../results/osm-based-city/0,5km2_performance_Routing.csv")
#  2. GeoJSON containing the graph-based routes (e.g. "./osm-city-routing.geojson")
#  2. GeoJSON containing the expected routes (e.g. "./osm-city-expected.geojson")

import sys
import glob
import geojson
import pyproj
import pandas as pd
from shapely.geometry import LineString
from shapely import transform

def round_beeline(distance_beeline):
    # All waypoints are multiple of 50m apart, therefore round to 50m to find match in hiker results
    return round(distance_beeline / 50) * 50

dataset=pd.read_csv(sys.argv[1])

features_routing = list()
with open(sys.argv[2]) as f:
    g = geojson.load(f)
    features_routing.extend(g["features"])

features_expected= list()
with open(sys.argv[3]) as f:
    g = geojson.load(f)
    features_expected.extend(g["features"])

# Change this to the UTM zone you're in
utm_projection = pyproj.Proj(proj="utm", zone=32, ellps="WGS84")
for f in features_routing:
    f["geometry"]["coordinates"] = [utm_projection.transform(c[0], c[1]) for c in f["geometry"]["coordinates"]]
for f in features_expected:
    f["geometry"]["coordinates"] = [utm_projection.transform(c[0], c[1]) for c in f["geometry"]["coordinates"]]

print("source,id,distance_beeline,distance_absolute,distance_relative")
for id in range(1, 11): # 1 to 10 (11 is exclusive)
    rounded_beeline_distance = 0
    distance_graph_routing = 0
    for f in features_routing:
        if str(id) == f["properties"]["id"]:
            coordinates = f["geometry"]["coordinates"]
            distance_graph_routing = LineString(coordinates).length
            routed_beeline_distance = LineString([coordinates[0], coordinates[-1]]).length
            rounded_beeline_distance = round_beeline(routed_beeline_distance)
            break

    distance_expected = 0
    for f in features_expected:
        if str(id) == f["properties"]["id"]:
            distance_expected = LineString(f["geometry"]["coordinates"]).length
            break

    measured_df = dataset[round_beeline(dataset["distance_beeline"]) == rounded_beeline_distance]
    measures_data = measured_df.reset_index().to_dict(orient='index')[0]

    distance_beeline = measures_data["distance_beeline"]
    distance_hiker = measures_data["distance_route"]

    print("expected,\"" + str(id) + "\"," + str(distance_beeline) + "," + str(distance_expected) + "," + str(distance_expected / distance_beeline))
    print("hiker,\"" + str(id) + "\"," + str(distance_beeline) + "," + str(distance_hiker) + "," + str(distance_hiker / distance_beeline))
    print("routing,\"" + str(id) + "\"," + str(distance_beeline) + "," + str(distance_graph_routing) + "," + str(distance_graph_routing / distance_beeline))
