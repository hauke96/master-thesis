#!/bin/bash

THESIS="../../../thesis"
TARGET="$THESIS/images/evaluation/"
FILES="
$(cat $THESIS/chapters/*.tex | grep --color=never ".*based.*\.pgf" | sed "s/.*\/\(.*based.*\/plot.*\.pgf\)}/\1/g")
$(cat $THESIS/chapters/*.tex | grep --color=never "evaluation/plot.*\.pgf" | sed "s/.*\/\(plot.*\.pgf\)}/\1/g")"

echo "Ensure target folder exist"
mkdir -p "$TARGET/osm-based-city"
mkdir -p "$TARGET/osm-based-rural"
mkdir -p "$TARGET/pattern-based-circles"
mkdir -p "$TARGET/pattern-based-rectangles"
mkdir -p "$TARGET/pattern-based-maze"

for f in $FILES
do
    echo "Copy $f    ->    $TARGET/$f"
    cp $f $TARGET/$f
done

echo "Done"
