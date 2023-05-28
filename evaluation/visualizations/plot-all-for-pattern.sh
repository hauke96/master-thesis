#!/bin/bash

set -e

# Usage: Only parameter is the pattern directory
if [[ -z $1 ]]
then
    echo "One argument expected: Name of result directory."
    echo
    echo "Example: pattern-based-rectangles"
    exit 1
fi

RESULT=$1

DATASET_GRAPH_GEN="../results/$RESULT/pattern_*_performance_GenerateGraph.csv"
DATASET_ROUTING="../results/$RESULT/pattern_*_performance_Routing.csv"

# $1 - File name (without .py)
# $2 - Dataset folder
# $3 - Title of the plot
function run()
{
    echo "Start script $1 on dataset $2."
    echo "> ./$1.py \"$2\" \"$3\""
    echo
    ./$1.py "$2" "$3"
    echo
    echo "=========="
    echo
}

# Graph generation
run "plot-iteration-details-per-vertices" "$DATASET_GRAPH_GEN" "HybridVisibilityGraph generation"
run "plot-iteration-time-per-vertices" "$DATASET_GRAPH_GEN" "HybridVisibilityGraph generation"

# Routing
run "plot-routing-length-factor" "$DATASET_ROUTING" "Ratio between beeline and actual route distance"
run "plot-routing-memory" "$DATASET_ROUTING" "Routing - Memory usage"
run "plot-routing-time-per-length" "$DATASET_ROUTING" "Routing - Time per distance"
run "plot-routing-time" "$DATASET_ROUTING" "Routing - Time"

echo "Done"
