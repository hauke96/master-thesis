import geojson
from geojson_length import calculate_distance, Unit

'''
Prints out the length of each line segment in the waypoints.geojson file in CSV format.
'''

with open('./datasets/waypoints.geojson') as f:
	featureCollection = geojson.load(f)
	coordinates = featureCollection['features'][0]['geometry']['coordinates']
	print("segment,length")
	for i in range(len(coordinates[1:])):
		line = geojson.Feature(geometry=geojson.LineString([coordinates[i], coordinates[i+1]]))
		print("%d,%.2f" % (i, calculate_distance(line, Unit.meters)))

