#/bin/bash

set -e

function process()
{
    OUTDIR="./dataset-hamburg/$1km2"
    mkdir -p $OUTDIR
    ogr2ogr "$OUTDIR/data.geojson" hamburg-latest-filtered.geojson -clipsrc "./bbox/$1km2.geojson" -overwrite
    cp "./waypoints/$1km2.geojson" "$OUTDIR/waypoints.geojson"
}

echo "Clean up existing files"
rm -rf hamburg-latest.osm.pbf \
    hamburg-latest-clipped.osm.pbf \
    hamburg-latest.geojson \
    hamburg-latest-filtered.geojson \
    dataset-hamburg

echo "Download raw OSM-dump"
wget https://download.geofabrik.de/europe/germany/hamburg-latest.osm.pbf

echo "Clip PBF by largest BBOX file (4km2)"
osmium extract --polygon ./bbox/4km2.geojson -o hamburg-latest-clipped.osm.pbf hamburg-latest.osm.pbf

echo "Convert PBF to GeoJSON"
osmium export hamburg-latest-clipped.osm.pbf -o hamburg-latest.geojson

echo "Extract features that are not underground"
ogr2ogr hamburg-latest-filtered.geojson hamburg-latest.geojson -where "\"level\" IS NULL OR \"level\"='0'" -overwrite

echo "Extract feature within BBOXes"
process "0,5"
process "1"
process "1,5"
process "2"
process "3"
process "4"

echo "Clean up intermediate files"
rm -rf hamburg-latest.osm.pbf \
    hamburg-latest-clipped.osm.pbf \
    hamburg-latest.geojson \
    hamburg-latest-filtered.geojson

echo "Done"
