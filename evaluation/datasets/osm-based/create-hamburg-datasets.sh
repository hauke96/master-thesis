#/bin/bash

set -e

echo "Clean up existing files"
rm -f hamburg-latest.osm.pbf \
    hamburg-latest.geojson \
    hamburg-latest-clip.geojson \
    hamburg-final.geojson \
    hamburg-final-simplified-1.geojson \
    hamburg-final-simplified-2.geojson \
    hamburg-final-simplified-3.geojson \
    hamburg-final-simplified-4.geojson \
    hamburg-final-simplified-5.geojson \
    hamburg-final-simplified-6.geojson

echo "Download raw OSM-dump"
wget https://download.geofabrik.de/europe/germany/hamburg-latest.osm.pbf

echo "Convert PBF to GeoJSON"
osmium export hamburg-latest.osm.pbf -o hamburg-latest.geojson

echo "Extract feature within bounding-polygon.geojson"
ogr2ogr hamburg-latest-clip.geojson hamburg-latest.geojson -clipsrc bounding-polygon.geojson -overwrite

echo "Extract features that are not underground"
ogr2ogr hamburg-final.geojson hamburg-latest-clip.geojson -where "\"level\" IS NULL OR \"level\"='0'" -overwrite

echo "Create simplified datasets"
ogr2ogr hamburg-final-simplified-1.geojson hamburg-final.geojson -simplify 0.0000001
ogr2ogr hamburg-final-simplified-2.geojson hamburg-final.geojson -simplify 0.000001
ogr2ogr hamburg-final-simplified-3.geojson hamburg-final.geojson -simplify 0.00001
ogr2ogr hamburg-final-simplified-4.geojson hamburg-final.geojson -simplify 0.000015
ogr2ogr hamburg-final-simplified-5.geojson hamburg-final.geojson -simplify 0.00003
ogr2ogr hamburg-final-simplified-6.geojson hamburg-final.geojson -simplify 0.0001
