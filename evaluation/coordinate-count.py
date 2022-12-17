#!/usr/bin/env python3

import geojson
import sys
from geojson_length import calculate_distance, Unit

'''
Prints out the number of coordinates (with and without duplicates).
'''

if len(sys.argv) - 1 != 1:
	print("Wrong number of arguments: Expected 1, found %s" % (len(sys.argv) - 1))
	print("Expected parameter: GeoJSON file")
	sys.exit(1)

# Normal coordinates
coordinates = []

# Coordinates of inner rings of MultiPolygons
innerPolygonCoordinates = []

with open(sys.argv[1]) as f:
	featureCollection = geojson.load(f)
	features = featureCollection['features']

	for feature in features:
		geometry = feature['geometry']
	
		if geometry['type'] == "MultiPolygon":
			allPolygons = geometry['coordinates']
			geomCoordinates = []
			for polygons in allPolygons:
				for polygon in polygons:
					coordinates.extend(polygon)
		elif geometry['type'] == "MultiLineString":
			allLineStrings = geometry['coordinates']
			for lineString in allLineStrings:
				coordinates.extend(lineString)
		elif geometry['type'] == "LineString":
			coordinates.extend(geometry['coordinates'])
		else:
			print("Found other geometry of type %s" % geometry['type'])

print("Number of coordinate (with duplicates): %d" % len(coordinates))

# Remove duplicates
coordinateTuples = set(tuple(c) for c in coordinates)
coordinates = [list(c) for c in coordinateTuples]
print("Number of coordinate (no duplicates):   %d" % len(coordinates))
