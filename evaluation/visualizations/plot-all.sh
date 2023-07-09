#!/bin/bash

set -e

# Usage: Only parameter is the pattern directory
if [[ -z $1 ]]
then
    echo "One argument expected: Name of result directory."
    echo
    echo "Example:"
    echo "./plot-all.sh pattern-based-rectangles"
    exit 1
fi

RESULT=$1

DATASET_GRAPH_GEN="../results/$RESULT/*_performance_GenerateGraph.csv"
DATASET_ROUTING="../results/$RESULT/*_performance_Routing.csv"

rm -f *.pgf *.png

# $1 - File name (without .py)
# $2 - Dataset folder
# $3 - Title of the plot
function run()
{
    echo "Start script $1 on dataset $2."
    echo "> ./$1.py \"$2\""
    echo
    ./$1.py "$2"
    echo
	echo "Move all output files to ./$RESULT"
	mv *.png $RESULT
	mv *.pgf $RESULT
#	mv *.pdf $RESULT
	echo
    echo "=========="
    echo
}

echo "Create output folder"
mkdir -p $RESULT

# Graph generation
run "plot-import-details-per-vertices" "$DATASET_GRAPH_GEN"
run "plot-import-time-per-vertices" "$DATASET_GRAPH_GEN"

# Routing
run "plot-routing-length-factor" "$DATASET_ROUTING"
run "plot-routing-time-details" "$DATASET_ROUTING"
run "plot-routing-time-per-length" "$DATASET_ROUTING"
run "plot-routing-time" "$DATASET_ROUTING"
run "plot-routing-memory" "$DATASET_ROUTING"

./plot-import-times-pattern-datasets.py

echo "Done"
