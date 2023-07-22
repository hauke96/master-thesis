#!/bin/bash

set -eu

# This script uses the OSM city datasets to measure their memory usage.
# Make sure the performance measurement is deactivated, no graphs are written to disk and the "Console.Read()" command in the ObstacleLayer is active.
#
# 1. Each dataset is copied to the HikerModel Resources folder
# 2. The model must be started until the PID is printed
# 3. Enter the PID of the model here
#
# This is repeated for every OSM city dataset.

DELAY=0.09 # 100ms minus a static 10ms offset for determining and writing the measurement

function record()
{
    cp datasets/osm-based/dataset-city/$1/waypoints.geojson ../code/HikerModel/Resources/waypoints.geojson
    cp datasets/osm-based/dataset-city/$1/$1.geojson ../code/HikerModel/Resources/obstacles.geojson

    echo "Dataset: $1"
    echo "Dataset files have been copied. Start the model until the PID is printed and the terminal of the model waits for confirmation."
    read -p "PID to record: " PID
    echo "Not proceed with the model."

    OUT="./ram-usage_$PID.csv"
    echo "Results will be written to $OUT"
    echo "Record..."

    echo "time,kb" > $OUT

    while true
    do
        sleep $DELAY
        TIME=$(($(date +%s%N)/1000000))
        RAM_USAGE_BYTES=$(ps -o pid,rss ax | grep "\s*$PID\s" | awk '{print $2}')
        if [[ -z $RAM_USAGE_BYTES ]]
        then
            echo "Process $PID ended"
            break
        fi
        echo "$TIME,$RAM_USAGE_BYTES" >> $OUT
    done

    RESULT="results/osm-based-city-ram/$1"
    mkdir -p $RESULT
    mv $OUT "$RESULT/memory.csv"

    read -p "Press ENTER when the timestamp CSV has been saved to proceed with the next dataset..."
    echo
}

record "0,5km2"
record "1km2"
record "1,5km2"
record "2km2"
record "3km2"
record "4km2"

echo "Done, no next dataset."
