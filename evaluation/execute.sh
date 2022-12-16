#!/usr/bin/env bash

# This Script executes a given model and renames each CSV file from the 
# performance measurement according to the dataset used.

set -e
# set -x

if [[ $# != 3 ]]
then
	cat<<END
ERROR: Unexpected number of arguments. Wanted 3, found $#.

END
fi

if [[ $# != 3 || $1 == "-h" || $1 == "--help" ]]
then
	cat<<END
Parameters: {model-dll}        {dataset-dir}  {dataset-names}
Example:    ./HikingModel.dll  ../datasets    "1 2 3 foo bar"

Output:
Each CSV file produces by the model will be renamed and gets the dataset-name as prefix.
END
	exit 1
fi

MODEL="$(basename $1)"
MODEL_DIR="$(dirname $1)"

DATASET_DIR="$(realpath $2)"
DATASETS="$3"

cd "$MODEL_DIR"

for d in $DATASETS
do
	# Copy the obstacle file to the right location
	cp "$DATASET_DIR/${d}.geojson" ./Resources/obstacles.geojson

	# Execute model which outputs CSV files with performance measurements
	dotnet $MODEL

	# Rename each CSV file to make clear what dataset was used
	echo "Add prefix '$d' to CSV files"
	for f in performance_*.csv
	do
		mv "$f" "${d}_$f"
	done
done
