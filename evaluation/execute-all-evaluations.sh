#!/usr/bin/env bash

set -e
# set -x

function head {
	echo
    printf '%*s\n' "${COLUMNS:-$(tput cols)}" '' | tr ' ' =
	echo " $1"
	time
	echo
}

# Rectangle datasets
head "Rectangle pattern datasets"
DATASETS=$(ls datasets/pattern-based-rectangles/ | grep --color=never -P "pattern_\\d*x\\d*\.geojson" | sed "s/\.geojson//g")
./execute-evaluation.sh ../code/HikerModel datasets/pattern-based-rectangles/ results/pattern-based-rectangles "$DATASETS"

# Maze datasets
head "Maze pattern datasets"
DATASETS=$(ls datasets/pattern-based-maze/ | grep --color=never -P "pattern_\\d*x\\d*\.geojson" | sed "s/\.geojson//g")
./execute-evaluation.sh ../code/HikerModel datasets/pattern-based-maze/ results/pattern-based-maze "$DATASETS"

# Circle datasets
head "Circle pattern datasets"
DATASETS=$(ls datasets/pattern-based-circles/ | grep --color=never -P "pattern_\\d*x\\d*\.geojson" | sed "s/\.geojson//g")
./execute-evaluation.sh ../code/HikerModel datasets/pattern-based-circles/ results/pattern-based-circles "$DATASETS"

# OSM "city"
head "OSM \"city\" datasets"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-city/0,5km2/ results/osm-based-city "0,5km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-city/1km2/ results/osm-based-city "1km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-city/1,5km2/ results/osm-based-city "1,5km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-city/2km2/ results/osm-based-city "2km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-city/3km2/ results/osm-based-city "3km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-city/4km2/ results/osm-based-city "4km2"

# OSM "rural"
head "OSM \"rural\" datasets"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-rural/0,5km2/ results/osm-based-rural "0,5km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-rural/1km2/ results/osm-based-rural "1km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-rural/1,5km2/ results/osm-based-rural "1,5km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-rural/2km2/ results/osm-based-rural "2km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-rural/3km2/ results/osm-based-rural "3km2"
./execute-evaluation.sh ../code/HikerModel datasets/osm-based/dataset-rural/4km2/ results/osm-based-rural "4km2"

# TODO city without roads

# TODO city without obstacles
