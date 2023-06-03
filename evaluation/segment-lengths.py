#!/usr/bin/env python3

import geojson
import sys
from geojson_length import calculate_distance, Unit

'''
Prints out the length of each line segment in the waypoints.geojson file in CSV format.
'''

if len(sys.argv) != 2:
	print("Wrong number of arguments: Expected 2, found %s" % len(sys.argv))
	sys.exit(1)

with open(sys.argv[1]) as f:
	featureCollection = geojson.load(f)
	coordinates = featureCollection['features'][0]['geometry']['coordinates']
	print("segment,length")
	for i in range(len(coordinates[1:])):
		line = geojson.Feature(geometry=geojson.LineString([coordinates[i], coordinates[i+1]]))
		print("%d,%.2f" % (i, calculate_distance(line, Unit.meters)))

