This document is as of: 2022-12-16 (commit 370041e514750494e6b41f14d6ceaefe9c6fe39a).

# Idea and Strategy

## Pure geometric routing performance

One part of the performance evaluation is the measurement of the pure routing performance (including the preprocessing).

Not all of the ideas and strategies might be implemented.

### Theoretic considerations

The optimum would be a Big-O notation of the algorithm.
A separate view on preprocessing and routing would be good.

Use these considerations as a kind of hypothesis for the experiments below (check if the experiment data behaves like the theoretic considerations predict it to behave).

TODO: These considerations are still to be done.

### Practical experiment

#### Strategy

1. Different datasets of different sizes
	1. Full dataset (OSM export, s. below), room dataset (pattern that can easily be scaled)
	2. Reduced datasets (delete objects): One with half and one with a fourth the vertices
	3. Reduce accuracy (simplify geometries). If possible: One with half and one with a fourth the vertices
2. Agent routing: Use the `HikierModel` with differently long distances between the waypoints (like 10m, 100m, 500m, 1000m, 2000m)

#### Data to collect

* Import time & memory consumption relative to amount of vertices
* Routing time & memory consumption relative to
	* Euclidean distance
	* Route distance
	* Amount of vertices
* Distance difference of euclidean and route distance (Kind of: Optimality of routing results)

#### Visualizations

1. Length of waypoint distances to show that they are evenly distributed
2. Vertex count of datasets
3. Import
	1. Time & memory consumption over vertex count
	1. Time & memory consumption per vertex over vertex count
	2. Separate graphs for the above metrics for `GetNeighborsFromObstacleVertices` and `CalculateVisibleKnn`
4. Routing
	1. Time & memory consumption relative to: Euclidean distance (time per m), route distance (time per m), amount of vertices (time per vertex)
	2. Routing time devided by import time over vertex count (How much work was moved into the import?)
	3. Factor of which the route was longer than the euclidean distance relative to the euclidean distance

## Simulation performance

TODO Plan to analyse simulation performance, routing overhead, etc.

# Datasets

## Create an OSM based dataset

See [./datasets/osm-based/README.md](README) for osm-based datasets.

## Create an artificial pattern based datasets

There's the `DatasetCreator` project.
It takes an area and the amount of pattern in x and y direction and the file `pattern.wkt` as input (see CLI argument description by just executing the tool without arguments).
The pattern is then scaled and repeated to fit exactly within the given area.

Coordinates will be snapped to each other (which connects near line strings) and coordinates near a line will be snapped to the closest point on that line (again connecting line strings).

# Run the evaluation

To run the evaluation, I used the CLI to be able to run the whole process using `sudo`.
This allows the model to set the thread priority to "high" so that the evaluation process runs more or less on its own thread.

## Preparations

1. Go into the `HikerModel` folder (or whatever model you want to use)
2. Make sure the hiker has a good step size for the dataset you're using (see `StepSize` in the `Model/Hiker.cs` class)
3. `dotnet build --configuration Release` (can also be executed from within the IDE)

## Run the model

Here you have two options:

A: Use a script executing a hole bunch of datasets and collecting their results

B: Manually execute the model with one dataset

### A: Using script

The Script `./evaluation/execute.sh` accepts the model path and parameter for the models themselves (see `-h` parameter for more information).
Running it like this uses the three smallest datasets from the pattern-datasets:

`evaluation/execute.sh code/HikerModel/bin/Release/net7.0/HikerModel.dll evaluation/datasets/pattern-based/ "1x1 2x2 3x3"`

Note: You must use `sudo` on Linux to change the thread priority to "high".

### B: Manual execution

4. Go into `bin/Release/net7.0/` of your model (or instead of `Release` and `net7.0` whatever configuration and .NET version you used)
5. Make sure the correct dataset is in the correct location (e.g. the correct GeoJSON file at `Resources/obstacles.geojson` for the HikingModel)
6. `sudo dotnet HikerModel.dll`

