This document is as of: 2022-12-16 (commit 370041e514750494e6b41f14d6ceaefe9c6fe39a).

# Idea and Strategy

The evaluation should three times:

1. Geometric routing only. This tests the preprocessing and routing performance of my code.
2. Network based routing only. This gives the basis to which my hybrid approach will be compared to.
3. The hybrid approach, which can be compared to the pure network and geometric approaches.

There will be two categories of datasets: Artificial datasets produced by a pattern and real world OSM-based datasets.
The OSM-based ones can be used in all three steps, the pattern one will only be used for the pure geometric and hybrid routing since no highways are in there, just obstacles.

## Data to collect

* Import time & memory consumption relative to amount of vertices
* Routing time & memory consumption relative to
	* Euclidean distance
	* Route distance
	* Amount of vertices
* Distance difference of euclidean and route distance (Kind of: Optimality of routing results)

## How each experiment is performed

The `HikierModel` will be used and the agent travels through the world based on the given file with waypoints.
The waypoints have different distances, e.g. 10m, 100m, 500m, 1000m, 2000m.

## Visualization

The following things should be visualized:

1. Length of waypoint distances to show that they are evenly distributed
2. Vertex count of datasets
3. Import
	1. Time & memory consumption over vertex count
	1. Time & memory consumption per vertex over vertex count
	2. Separate graphs for the above metrics for `GetNeighborsFromObstacleVertices` and `CalculateVisibleKnn`
4. Routing
	1. Time & memory consumption relative to: Euclidean distance (time per m), route distance (time per m), amount of vertices (time per vertex)
	2. Routing time divided by import time over vertex count (How much work was moved into the import?)
	3. Factor of which the route was longer than the euclidean distance relative to the euclidean distance

### Tool

TODO: Find and document visualization tool. Make the usage as automated as possible. Ideally one script should date all the input data and generate all necessary diagrams.

## Evaluation checklist

This is a list of evaluations that should be performed categorized by dataset.
Details on each dataset can be found below.

### 1. Pure geometric routing

* [ ] Maze like pattern
	* [ ] Collect data
	* [ ] Visualize
* [ ] Square pattern
	* [ ] Collect data
	* [ ] Visualize
* [ ] Circle pattern
	* [ ] Collect data
	* [ ] Visualize
* [ ] Full OSM dataset
	* [ ] Collect data
	* [ ] Visualize
* [ ] OSM dataset with 1/2 the objects
	* [ ] Collect data
	* [ ] Visualize
* [ ] OSM dataset with 1/4 the objects
	* [ ] Collect data
	* [ ] Visualize

### 2. Pure network routing

* [ ] Full OSM dataset
	* [ ] Collect data
	* [ ] Visualize
* [ ] OSM dataset with 1/2 the objects
	* [ ] Collect data
	* [ ] Visualize
* [ ] OSM dataset with 1/4 the objects
	* [ ] Collect data
	* [ ] Visualize

TODO Clearify the reduced dataset: Just make highways less accurate?

### 3. Hybrid routing

* [ ] Maze like pattern
	* [ ] Collect data
	* [ ] Visualize
* [ ] Square pattern
	* [ ] Collect data
	* [ ] Visualize
* [ ] Circle pattern
	* [ ] Collect data
	* [ ] Visualize
* [ ] Full OSM dataset
	* [ ] Collect data
	* [ ] Visualize
* [ ] OSM dataset with 1/2 the objects
	* [ ] Collect data
	* [ ] Visualize
* [ ] OSM dataset with 1/4 the objects
	* [ ] Collect data
	* [ ] Visualize

## Theoretic considerations

The optimum would be a Big-O notation of the algorithm.
A separate view on preprocessing and routing would be good.

Use these considerations as a kind of hypothesis for the experiments below (check if the experiment data behaves like the theoretic considerations predict it to behave).

TODO: These considerations are still to be done.

# Datasets

## Create an OSM based dataset

See [./datasets/osm-based/README.md](README) for osm-based datasets.

## Create an artificial pattern based datasets

TODO: Maybe move this documentation to the `datasets/pattern-based` folder?

There's the `DatasetCreator` project.
Execute the `DatasetCreator.dll` without parameters to get usage information.

This tool takes an area and the amount of pattern in x and y direction and the file `pattern.wkt` as input (see CLI argument description by just executing the tool without arguments).
The pattern is then scaled and repeated to fit exactly within the given area.

Coordinates will be snapped to each other (which connects near line strings) and coordinates near a line will be snapped to the closest point on that line (again connecting line strings).

# Run the evaluation

TODO Rework this and see if it still applies, is complete and correct.

To run the evaluation, I used the CLI to be able to run the whole process using `sudo`.
This allows the model to set the thread priority to "high" so that the evaluation process runs more or less on its own thread.

## Build the model

1. Go into the `HikerModel` folder (or whatever model you want to use)
2. Make sure the hiker has a good step size for the dataset you're using (see `StepSize` in the `Model/Hiker.cs` class)
3. `dotnet build --configuration Release` (can also be executed from within the IDE)

## Run the model

Here you have two options which are described in detail below:

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

