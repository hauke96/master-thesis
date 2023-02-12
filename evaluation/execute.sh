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

Parameters: {model-dll}        {dataset-dir}   {result-dir}   {dataset-names}
Example:    ./HikingModel.dll  ./datasets/foo  ./results/foo  "1 2 3 foo bar"

This Script executes a given model with the given datasets. Resulting CSV files from the model folder will get the pattern name as prefix and are moved to the given result folder.

The datasets must be GeoJSON files but the given names must *not* contain the ".geojson" ending.
END
	exit 1
fi

MODEL="$(basename $1)"
MODEL_DIR="$(dirname $1)"

DATASET_DIR="$(realpath $2)"
RESULT_DIR="$(realpath $3)"
DATASETS="$4"

echo "Load model $MODEL from $MODEL_DIR"
echo "Use datasets from $DATASET_DIR"
echo "Save results into $RESULT_DIR when done"

cd "$MODEL_DIR"

for d in $DATASETS
do
	hline "  $d"

	# Copy the obstacle file to the right location
	cp "$DATASET_DIR/${d}.geojson" ./Resources/obstacles.geojson

	# Execute model which outputs CSV files with performance measurements
	dotnet $MODEL

	# Rename each CSV file to make clear what dataset was used
	echo "Add prefix '$d' to CSV files"
	for f in performance_*.csv
	do
		OUT="$RESULT_DIR/${d}_$f"
		echo "Move result $f to $OUT"
		mv "$f" "$OUT"
	done
done
