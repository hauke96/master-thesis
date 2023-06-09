#/bin/bash

set -e

TYPE="$1"

# $1: Type of dataset (e.g. "city" for "bbox-city" and "waypoints-city")
# $2: Specific bbox- and waypoint file (e.g. "3km2")
function process()
{
	echo "Process $2km2 dataset fpr '$1'"
    OUTDIR="./dataset-$1/$2km2"
    mkdir -p $OUTDIR
    ogr2ogr "$OUTDIR/$2km2.geojson" hamburg-latest-filtered.geojson -clipsrc "./bbox-$1/$2km2.geojson" -overwrite
    cp "./waypoints-$1/$2km2.geojson" "$OUTDIR/waypoints.geojson"
}

echo "Clean up existing files"
rm -rf hamburg-latest.osm.pbf \
    hamburg-latest-clipped.osm.pbf \
    hamburg-latest.geojson \
    hamburg-latest-filtered.geojson \
    dataset-hamburg

echo "Download raw OSM-dump"
wget https://download.geofabrik.de/europe/germany/hamburg-latest.osm.pbf
echo

for TYPE in "city rural"
do
	echo "=========="
	echo "  $TYPE"
	echo "=========="
	
	echo "Clip PBF by largest BBOX file (4km2)"
	osmium extract --polygon "./bbox-$TYPE/4km2.geojson" -o hamburg-latest-clipped.osm.pbf hamburg-latest.osm.pbf
	
	echo "Convert PBF to GeoJSON"
	osmium export hamburg-latest-clipped.osm.pbf -o hamburg-latest.geojson
	
	echo "Extract features that are not underground"
	ogr2ogr hamburg-latest-filtered.geojson hamburg-latest.geojson -where "\"level\" IS NULL OR \"level\"='0'" -overwrite
	
	echo "Extract feature within BBOXes"
	process $TYPE "0,5"
	process $TYPE "1"
	process $TYPE "1,5"
	process $TYPE "2"
	process $TYPE "3"
	process $TYPE "4"
	
	echo "Created datasets for '$TYPE'"
	echo
done
	
echo "Clean up intermediate files"
rm -rf hamburg-latest.osm.pbf \
    hamburg-latest-clipped.osm.pbf \
    hamburg-latest.geojson \
    hamburg-latest-filtered.geojson

echo "Done"
