#!/bin/bash

set -e

rm -f *.pgf *.png

function adjust_pgf()
{
    find $1 -type f -name "*.pgf" -exec sed -i 's/bounding box, clip/bounding box/g' {} \;
    find $1 -type f -name "*.pgf" -exec sed -i '/^%%/d' {} \;
}

DATASETS="osm-based-city osm-based-rural pattern-based-rectangles pattern-based-circles pattern-based-maze"
for d in $DATASETS
do
    echo "===================="
    echo "  $d"
    echo "===================="
    ./plot-dataset.sh $d
    adjust_pgf $d
done

DATASETS="osm-based-city osm-based-rural"
for d in $DATASETS
do
    echo "===================="
    echo "  $d (no-roads/no-obstacles)"
    echo "===================="
    echo "Create output folder"
    mkdir -p $d
    ./plot-no-roads-obstacles.py "../results/$d"
    adjust_pgf .
    mv *.png $d
    mv *.pgf $d
done

echo "===================="
echo "  Combined pattern-based plot"
echo "===================="
./plot-times-pattern-datasets.py
adjust_pgf .

echo "Done"
