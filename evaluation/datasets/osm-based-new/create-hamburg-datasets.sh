#/bin/bash

set -e

function clip()
{
    ogr2ogr "hamburg-$1km2.geojson" hamburg-latest-filtered.geojson -clipsrc "bbox-$1km2.geojson" -overwrite
}

echo "Clean up existing files"
rm -f hamburg-latest.osm.pbf \
    hamburg-latest.geojson \
    hamburg-latest-filtered.geojson \
    hamburg-*km2.geojson

echo "Download raw OSM-dump"
wget https://download.geofabrik.de/europe/germany/hamburg-latest.osm.pbf

echo "Convert PBF to GeoJSON"
osmium export hamburg-latest.osm.pbf -o hamburg-latest.geojson

echo "Extract features that are not underground"
ogr2ogr hamburg-latest-filtered.geojson hamburg-latest.geojson -where "\"level\" IS NULL OR \"level\"='0'" -overwrite

echo "Extract feature within BBOXes"
clip "0,5"
clip "1"
clip "1,5"
clip "2"
clip "3"
clip "4"
