#!/usr/bin/env bash

set -e
# set -x

function hline {
	echo
    printf '%*s\n' "${COLUMNS:-$(tput cols)}" '' | tr ' ' =
	echo " $1"
    printf '%*s\n' "${COLUMNS:-$(tput cols)}" '' | tr ' ' =
	echo
}

if [[ $# != 4 || $1 == "-h" || $1 == "--help" ]]
then
	if [[ $1 != "-h" && $1 != "--help" ]]
	then
		echo "ERROR: Unexpected number of arguments. Wanted 4, found $#."
		echo
	fi

	cat<<END
Execute multiple datasets on a given model.

Parameters: {model-dll}        {dataset-dir}  {result-dir}  {dataset-names}
Example:    models/HikerModel  datasets/foo   results/foo   "1 2 3 foo bar"

This script does the following:
 1. Clean-build the model in "Release" configuration
 2. Copy each given dataset to the "Resources" folder in the built model (so into "bin/Release/.../Resources/obstacles.geojson")
 3. Copy the "{dataset-dir}/waypoints.geojson" to the "Resources" folder
 4. Execute the model
 5. Move all *.csv files to the given result folder and add the "performance_{dataset-name}" as prefix

The datasets must be GeoJSON files but the given names must *not* contain the ".geojson" ending.
END
	exit 1
fi

MODEL="$(basename $1)"
MODEL_DIR="$1"

BUILD_CONFIGURATION="Release"
BUILD_DIR="bin/$BUILD_CONFIGURATION/net7.0"

DATASET_DIR="$(realpath $2)"
RESULT_DIR="$(realpath $3)"
DATASETS="$4"

echo "Load model $MODEL from $MODEL_DIR"
echo "Use datasets from $DATASET_DIR"
echo "Save results into $RESULT_DIR when done"

echo "Ensure result folder exists"
mkdir -p $RESULT_DIR

echo "Go into model dir $MODEL_DIR"
cd "$MODEL_DIR"

echo "Build model in '$BUILD_CONFIGURATION' configuration"
dotnet clean
dotnet build --configuration Release

echo "Go into build dir $BUILD_DIR"
cd $BUILD_DIR

echo "Start executing each dataset"
for d in $DATASETS
do
	hline "  $d"

	# Copy the obstacle file to the right location
	cp "$DATASET_DIR/${d}.geojson" ./Resources/obstacles.geojson
	cp "$DATASET_DIR/waypoints.geojson" ./Resources/waypoints.geojson

	# Execute model which outputs CSV files with performance measurements
	dotnet "${MODEL}.dll"

	# Rename each CSV file to make clear what dataset was used
	echo "Add prefix '$d' to CSV files"
	for f in performance_*.csv
	do
		OUT="$RESULT_DIR/${d}_$f"
		echo "Move result $f to $OUT"
		mv "$f" "$OUT"
	done
done

hline "  DONE"
