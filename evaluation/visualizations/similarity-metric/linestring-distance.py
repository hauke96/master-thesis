#!/bin/python

# This script calculates the frechet distance for the linestrings with the same "id" attribute.

import sys
import glob
import geojson
import pyproj
from shapely.geometry import LineString
from shapely import transform, hausdorff_distance, from_geojson

if len(sys.argv) - 1 == 0:
    print("ERROR: At least one GeoJSON file must be specified!")
    sys.exit(1)

# Change this to the UTM zone you're in
utm_projection = pyproj.Proj(proj="utm", zone=32, ellps="WGS84")

def ensure_num_coordinates(coordinates, num_points=20):
    existing_line = LineString(coordinates)
    num_coords_to_add = num_points - len(existing_line.coords)
    total_length = existing_line.length

    # Calculate the distance between each interpolated point
    distance_between_points = total_length / (num_points - 1)
    #print("distance_between_points="+str(distance_between_points))

    # Interpolate between each segment to create the needed number of coordinates
    interpolated_coordinates = []
    for i in range(len(coordinates)-1):
        coordinate = coordinates[i]
        segment = LineString([coordinate, coordinates[i+1]])
        points_to_add = segment.length / total_length * num_points - 1
        interpolated_coordinates.append(coordinate)
        #print(str(coordinate) + " with points_to_add="+str(points_to_add) + " ("+str(int(points_to_add))+")")
        if points_to_add >= 1:
            for i in range(1, round(points_to_add) + 1):
                distance_along_line = i * distance_between_points
                interpolated_point = segment.interpolate(distance_along_line)
                interpolated_coordinates.append(interpolated_point.coords[0])

    # Add last coordinate, which is not added during the above loop
    interpolated_coordinates.append(coordinates[-1])
    return interpolated_coordinates

features = list()
for i in range(len(sys.argv[1:])):
    with open(sys.argv[i+1]) as f:
        #print("Read file: "+f.name)
        g = geojson.load(f)
        features.extend(g["features"])

#print("Read "+str(len(features))+" features")

for f in features:
    f["geometry"]["coordinates"] = [utm_projection.transform(c[0], c[1]) for c in f["geometry"]["coordinates"]]

interpolated_geometries = [ensure_num_coordinates(g["geometry"]["coordinates"]) for g in features]

for i in range(len(interpolated_geometries)):
    features[i]["geometry"]["coordinates"] = interpolated_geometries[i]

feature_map = dict()
for f in features:
    if not "id" in f["properties"]:
        continue
    id = f["properties"]["id"]
    if not id in feature_map:
        feature_map[id] = list()
    feature_map[id].append(f)

print("id,h_dist")
for id, features in feature_map.items():
    if len(features) != 2:
        #print("Features with ID "+str(id)+": "+str(len(features)))
        continue
    f_a = features[0]
    f_b = features[1]
    h_dist = hausdorff_distance(LineString(f_a["geometry"]["coordinates"]), LineString(f_b["geometry"]["coordinates"]))
    #print("==================")
    #print(str(len(f_a["geometry"]["coordinates"])))
    #print(f_a["geometry"]["coordinates"])
    #print()
    #print(str(len(f_b["geometry"]["coordinates"])))
    #print(f_b["geometry"]["coordinates"])
    #print("ID: "+str(id))
    #print("  Hausdorff distance: "+str(h_dist))
    print(str(id)+","+str(h_dist))
