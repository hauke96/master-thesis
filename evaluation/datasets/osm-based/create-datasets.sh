#/bin/bash

set -e

TYPE="$1"

# $1: Type of dataset (e.g. "city" for "bbox-city" and "waypoints-city")
# $2: Specific bbox- and waypoint file (e.g. "3km2")
function process()
{
	echo "Process $2km2 dataset for '$1'"
    OUTDIR="./dataset-$1/$2km2"
    mkdir -p $OUTDIR
    ogr2ogr "$OUTDIR/$2km2.geojson" hamburg-latest-filtered.geojson -clipsrc "./bbox-$1/$2km2.geojson" -overwrite
    cp "./waypoints-$1/$2km2.geojson" "$OUTDIR/waypoints.geojson"
}

function cleanup-intermediate()
{
	echo "Clean up intermediate files"
	rm -rf hamburg-latest-clipped.osm.pbf \
		hamburg-latest.geojson \
	    hamburg-latest-filtered.geojson
}

cleanup-intermediate

if [ ! -f ./hamburg-latest.osm.pbf ]
then
	echo "Download raw OSM-extract"
	wget https://download.geofabrik.de/europe/germany/hamburg-latest.osm.pbf
else
	echo "Re-use existing extract file"
fi

for TYPE in "city" "rural"
do
	echo "=========="
	echo "  $TYPE"
	echo "=========="

	echo "Clean up existing dataset folder"
	rm -rf "./dataset-$TYPE"
	
	echo "Clip PBF by largest BBOX file (4km2)"
	osmium extract --polygon "./bbox-$TYPE/4km2.geojson" -o hamburg-latest-clipped.osm.pbf hamburg-latest.osm.pbf --overwrite
	
	echo "Convert PBF to GeoJSON"
	osmium export hamburg-latest-clipped.osm.pbf -o hamburg-latest.geojson --overwrite

	if grep -q "\"level\":" hamburg-latest.geojson
	then
		echo "Extract features that are not underground"
		ogr2ogr hamburg-latest-filtered.geojson hamburg-latest.geojson -where "\"level\" IS NULL OR \"level\"='0'" -overwrite
	else
		echo "No 'level' in GeoJSON-file -> no filtering"
		cp hamburg-latest.geojson hamburg-latest-filtered.geojson
	fi
	
	echo "Extract feature within BBOXes"
	process $TYPE "0,5"
	process $TYPE "1"
	process $TYPE "1,5"
	process $TYPE "2"
	process $TYPE "3"
	process $TYPE "4"
	
	cleanup-intermediate

	echo "Created datasets for '$TYPE'"
	echo
done
echo

echo "Done"
