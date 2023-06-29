This folder contains the OSM based datasets and their waypoints.
Each dataset file covers a certain area, e.g. the `3km2` file covers a 3 km² large area.
For each such files exists a waypoint file, e.g. `waypoints-3km2.geojson` for the mentiones dataset file.

Additionally, several types of datasets can be created, e.g. the "city" and "rural" types.
They are structures in the same way, just their location is different so that they cover different types of landscapes and obstacles.

# Requirements

The following tools must be installed. Arch Linux package names in parentheses:

* `grep` (`grep`)
* `wget` (`wget`)
* `ogr2ogr` (`ogr2ogr`)
* `osmium` (`osmium-tool`)
* `osmtogeojson` (`osmtogeojson`)

# Creation

The files can be (re)created using the `create-datasets.sh` script.
It automatically downloads an OSM dump of Hamburg, Germany, and directly cuts it into the needed dataset files.

The script create sub-folders for each dataset type.
In each type-folder (e.g. "dataset-city") sub-folder for each area exist, because the execution script for the evaluation expects *one* `waypoints.geojson` file for all datasets.
This is not the case here since each dataset file has its own waypoint file, hence separate folders are needed.

## Filtering

The data gets filtered, so no under- and overground objects are within the resulting datasets.
This is done due to the fact that the hybrid routing algorithm has no special handling for this third spatial dimension.
