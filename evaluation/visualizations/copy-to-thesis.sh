#!/bin/bash

THESIS="../../../thesis"
TARGET="$THESIS/images/evaluation/"
FILES=$(cat ../../../thesis/chapters/*.tex | grep --color=never -oP "evaluation/(.*\.pgf)" | sed "s/evaluation\///g")

echo "Ensure target folder exist"
mkdir -p "$TARGET/osm-based-city"
mkdir -p "$TARGET/osm-based-rural"
mkdir -p "$TARGET/pattern-based-circles"
mkdir -p "$TARGET/pattern-based-rectangles"
mkdir -p "$TARGET/pattern-based-maze"
mkdir -p "$TARGET/similarity-metric"

for f in $FILES
do
    echo "Copy $f    ->    $TARGET/$f"
    cp $f $TARGET/$f
done

echo "Done"
