#/bin/bash

set -e

function head {
	echo
    printf '%*s\n' "${COLUMNS:-$(tput cols)}" '' | tr ' ' =
	echo " $1"
    printf '%*s\n' "${COLUMNS:-$(tput cols)}" '' | tr ' ' =
	date
	echo
}

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
	rm -rf hamburg-latest-clipped* \
	    hamburg-latest-filtered*
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
	head "$TYPE"

	echo "Clean up existing dataset folder"
	rm -rf "./dataset-$TYPE"
	
	echo "Clip PBF by largest BBOX file (4km2)"
	osmium extract --polygon "./bbox-$TYPE/4km2.geojson" -o hamburg-latest-clipped.osm hamburg-latest.osm.pbf --overwrite
	
	echo "Convert OSM to GeoJSON"
	osmtogeojson hamburg-latest-clipped.osm > hamburg-latest-clipped.geojson

	if grep -q "\"level\":" hamburg-latest-clipped.geojson
	then
		echo "Extract features, which are not underground"
		ogr2ogr hamburg-latest-filtered.geojson hamburg-latest-clipped.geojson -where "\"level\" IS NULL OR \"level\"='0'" -overwrite
	else
		echo "No 'level' in GeoJSON-file -> no filtering"
		cp hamburg-latest-clipped.geojson hamburg-latest-filtered.geojson
	fi
	
	echo "Extract feature within BBOXes"
	process $TYPE "0,5"
	process $TYPE "1"
	process $TYPE "1,5"
	process $TYPE "2"
	process $TYPE "3"
	process $TYPE "4"
	
	echo "$TYPE - Without roads"
	IN=dataset-$TYPE
	OUT=$IN-no-roads
	rm -rf $OUT
	mkdir $OUT
	if grep -q "\"tunnel\":" hamburg-latest-clipped.geojson
	then
		echo "Add tunnel filtering to query"
		TUNNEL_QUERY="OR tunnel IS NOT NULL"
	else
		echo "No tunnel values exist, so no additional filtering will be added"
		TUNNEL_QUERY=""
	fi
	ogr2ogr $OUT/data.geojson $IN/4km2/4km2.geojson -where "highway IS NULL $TUNNEL_QUERY"
	cp $IN/4km2/waypoints.geojson $OUT/
	
	echo "$TYPE - Without obstacles"
	IN=dataset-$TYPE
	OUT=$IN-no-obstacles
	rm -rf $OUT
	mkdir $OUT
	ogr2ogr $OUT/data.geojson $IN/4km2/4km2.geojson -where "highway IS NOT NULL"
	cp $IN/4km2/waypoints.geojson $OUT/
	
	echo "Cleanup"
	cleanup-intermediate

	echo "Created datasets for '$TYPE'"
	echo
done

echo "Done"
