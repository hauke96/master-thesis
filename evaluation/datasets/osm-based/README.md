This folder contains the OSM based datasets and their waypoints.
Each dataset file covers a certain area, e.g. the `3km2` file covers a 3 km² large area.
For each such files exists a waypoint file, e.g. `waypoints-3km2.geojson` for the mentiones dataset file.

# Creation

The files can be (re)created using the `create-hamburg-dataset.sh` script.
It automatically downloads an OSM dump of Hamburg, Germany, and directly cuts it into the needed dataset files.

The script create sub-folders for each dataset file because the execution script for the evaluation expects *one* `waypoints.geojson` file for all datasets.
This is not the case here since each dataset file has its own waypoint file.

## Filtering

The data gets filtered, so no under- and overground objects are within the resulting datasets.
This is done due to the fact that the hybrid routing algorithm has no special handling for this third spatial dimension.

## Requirements

Some external tools are needed for this script:

* `wget`
* `osmium`
* `ogr2ogr`
