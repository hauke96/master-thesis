This document is as of: 2023-06-09 (commit 1b64146857cc69089fa11d1f56b458f35d6ded9f).

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

Visualization is done by python scripts using the seaborn library. See the according [README](./visualization/README.md) for further details.

## Evaluation checklist

This is a list of evaluations that should be performed categorized by dataset.
Details on each dataset can be found below.

### 1. Without roads

The graph generation and routing is performed as is (without adjustments).
This means the merging takes place (even though there are no roads) and routing is done on the resulting hybrid visisibility graph.

* [x] Maze like pattern
	* [x] Create dataset
	* [x] Measurement
	* [x] Visualize
* [x] Rectangle pattern
	* [x] Create dataset
	* [x] Measurement
	* [x] Visualize
* [x] Circle pattern
	* [x] Create dataset
	* [x] Measurement
	* [x] Visualize
* [ ] OSM "city" dataset without roads (multiple area sizes)
	* [x] Create dataset
	* [x] Measurement
	* [ ] Visualize
* [ ] OSM "rural" dataset without roads (multiple area sizes)
	* [x] Create dataset
	* [x] Measurement
	* [ ] Visualize

### 2. With roads

The import takes place as is, without adjustments. Merging is performed and routing takes place on the resulting visisibility graph.

* [x] OSM "city" dataset with roads (multiple area sizes)
	* [x] Create dataset
	* [x] Measurement
	* [x] Visualize
* [x] OSM "rural" dataset with roads (multiple area sizes)
	* [x] Create dataset
	* [x] Measurement
	* [x] Visualize

### 3. Without obstacles

The import takes place but without obstacle considerstion. The dataset contains them, but `GetObstacles` is modified to return an empty set.
Merging is performed and routing takes place on the resulting visisibility graph.

* [ ] OSM "city" dataset without obstacles (multiple area sizes)
	* [x] Create dataset
	* [x] Measurement
	* [ ] Visualize
* [ ] OSM "rural" dataset without obstacles (multiple area sizes)
	* [x] Create dataset
	* [x] Measurement
	* [ ] Visualize

### 4. Optimizations

Use the full OSM dataset and run it with different optimizations turned on/off:

* [ ] Shadow areas
	* [ ] Measurement
	* [ ] Visualize
* [ ] Convex hull (only consider vertices on convex hull)
	* [ ] Measurement
	* [ ] Visualize
* [ ] Convex hull (only consider valid angle areas)
	* [ ] Measurement
	* [ ] Visualize
* [ ] Convex hull (both)
	* [ ] Measurement
	* [ ] Visualize
* [ ] BinTree instead of BinIndex
	* [ ] Measurement
	* [ ] Visualize
* [ ] Normal NTS collision detection instead of my implementation
	* [ ] Measurement
	* [ ] Visualize
* [ ] Without knn restriction (= search for all visibility neighbors)
	* [ ] Measurement
	* [ ] Visualize

## Theoretic considerations

The optimum would be a Big-O notation of the algorithm.
A separate view on preprocessing and routing would be good.

Use these considerations as a kind of hypothesis for the experiments below (check if the experiment data behaves like the theoretic considerations predict it to behave).

TODO: These considerations are still to be done.

# Datasets

## Create an OSM based dataset

See the [README](./datasets/osm-based/README.md) for osm-based datasets.

## Create an artificial pattern based datasets

See the general [README](./datasets/README.md) for datasets.

# Run the evaluation

To run the evaluation, I used the CLI to be able to run the whole process using `sudo`.
This allows the model to set the thread priority to "high" so that the evaluation process runs more or less on its own thread.

## Build the model

1. Go into the `HikerModel` folder (the hole process is tailored for this project)
2. Make sure the hiker has a good step size for the dataset you're using (see `StepSize` in the `Model/Hiker.cs` class)
3. `dotnet build --configuration Release` (can also be executed from within the IDE)

## Run the model

There are two options which are described in detail below:

* A: Use a script executing a hole bunch of datasets and collecting their results
* B: Manually execute the model with one dataset

### A: Using script

The Script `./evaluation/execute-evaluation.sh` accepts the model path and parameter for the models themselves (see `-h` parameter for more information).
Running it like this uses the three smallest datasets from the pattern-datasets:

```
execute-evaluation.sh ../code/HikerModel datasets/pattern-based-rectangles results/pattern-based-rectangles "pattern_1x1 pattern_2x2 pattern_3x3"
```

Little helper script to not manually copy-paste all dataset names:

```
DATASETS=$(ls datasets/pattern-based-rectangles/ | grep --color=never -P "pattern_\\d*x\\d*\.geojson" | sed "s/\.geojson//g")
sudo ./execute-evaluation.sh ../code/HikerModel datasets/pattern-based-rectangles results/pattern-based-rectangles/ "$DATASETS"
```

**Note:** You must use `sudo` on Linux to change the thread priority to "high".

#### Getting dataset files

Specifying all dataset names manually is boring, so here's some helping code getting all relevant GeoJSON files for the rectangle pattern based dataset:

`DATASETS=$(ls datasets/pattern-based-rectangles/ | grep --color=never -P "pattern_\\d*x\\d*\.geojson" | sed "s/\.geojson//g")`

Using `./execute.sh ... $DATASETS` is much easier.

### B: Manual execution

Within the `code/HikerModel` folder:

1. Go into `bin/Release/net7.0/` of your model (or instead of `Release` and `net7.0` whatever configuration and .NET version you used)
2. Make sure the correct dataset is in the resources folder (i.e. the correct GeoJSON file at `Resources/obstacles.geojson` within the current `bin/...` folder)
2. Make sure the correct waypoints are in the resources folder (i.e. the correct GeoJSON file at `Resources/waypoints.geojson` within the current `bin/...` folder)
3. `sudo dotnet HikerModel.dll`

# Visualize the results

The `visualizations/` folder contains several python sripts generating visualizations via the python library seaborn.




