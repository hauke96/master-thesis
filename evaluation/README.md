**Note:**
This document is as of: 2023-07-25 (commit 0460b47688e1b8a483a89b626f4019b8866f6b7b).

# Idea and Strategy

There will be two categories of datasets: Patter pattern-based datasets and real-world OSM-based datasets.
One agent (s. `code/HikerModel`) is used with an agent creating and walking along numerous routes in a given dataset.

## Data to collect

* Import time relative to amount of vertices
* Routing time relative to
	* Euclidean distance
	* Route distance
	* Amount of vertices
	* Amount of edges
* Times of different methods (i.e. not only the total time of the algorithm/routing)
* General memory consumption (via additional `record-ram-usage.sh` script)

## How each experiment is performed

The `HikierModel` will be used and the agent travels through the world based on the given file with waypoints of different distances.

## Visualization

Numerous things can then be visualized, e.g. the processing time of each task or the relation between the beeline and actual distance.

Visualization is done by python scripts using the `seaborn` library.
See the according [README](./visualization/README.md) for further details.

## Evaluations

This is a list of evaluations that were performed, categorized by dataset.
Details on each dataset can be found below.

### 1. Without roads

The graph generation and routing is performed as is (without adjustments).
This means the merging takes place (even though there are no roads) and routing is done on the resulting hybrid visisibility graph.

* Maze like pattern
* Rectangle pattern
* Circle pattern
* OSM "city" dataset without roads (multiple area sizes)
* OSM "rural" dataset without roads (multiple area sizes)

### 2. With roads

The import takes place as is, without adjustments. Merging is performed and routing takes place on the resulting visisibility graph.

* OSM "city" dataset with roads (multiple area sizes)
* OSM "rural" dataset with roads (multiple area sizes)

### 3. Without obstacles

The import takes place but without obstacle, they got removed in beforehand.
Merging is performed and routing takes place on the resulting visisibility graph.

* OSM "city" dataset without obstacles (multiple area sizes)
* OSM "rural" dataset without obstacles (multiple area sizes)

### 4. Optimizations

The full OSM dataset were usef with different optimizations turned on/off:

* Shadow areas
* Convex hull filtering
* Valid angle area filtering
* BinTree instead of BinIndex
* Normal NTS collision detection instead of my implementation
* Without knn restriction (= search for all visibility neighbors)

# Datasets

## Create an OSM based dataset

See the [README](./datasets/osm-based/README.md) for osm-based datasets.

## Create an artificial pattern-based datasets

See the general [README](./datasets/README.md) for datasets.

# Run the evaluation

To run the evaluation, I used the CLI to be able to run the whole process using `sudo`.
This allows the model to set the thread priority to "high" so that the evaluation process runs more or less on its own thread.
This is optional, the model worls without `sudo` as well.

## Build the model

1. Go into the `HikerModel` folder (the hole process is tailored for this project)
2. `dotnet build --configuration Release` (can also be executed from within the IDE)

## Run the model

There are two options which are described in detail below:

* A: Use a script executing a hole bunch of datasets and collecting their results
* B: Manually execute the model with one dataset

### A: Using script

The `execute-all-evaluations.sh`, as the name suggests, executed all evaluations using all datasets.
It internally uses the `execute-evaluation.sh` script.

The Script `execute-evaluation.sh` accepts the model path and parameter for the models themselves (see `-h` parameter for more information).
Running it like this uses the three smallest datasets from the pattern-datasets:

```
execute-evaluation.sh ../code/HikerModel datasets/pattern-based-rectangles results/pattern-based-rectangles "pattern_1x1 pattern_2x2 pattern_3x3"
```

Little helper script to not manually copy-paste all dataset names as used in the `all-evaluations`-script:

```
DATASETS=$(ls datasets/pattern-based-rectangles/ | grep --color=never -P "pattern_\\d*x\\d*\.geojson" | sed "s/\.geojson//g")
sudo ./execute-evaluation.sh ../code/HikerModel datasets/pattern-based-rectangles results/pattern-based-rectangles/ "$DATASETS"
```

**Note:** You must use `sudo` on Linux to change the thread priority to "high" (but it will work without it as well).

### B: Manual execution

Within the `code/HikerModel` folder:

1. Go into `bin/Release/net7.0/` of your model (or instead of `Release` and `net7.0` whatever configuration and .NET version you used)
2. Make sure the correct dataset is in the resources folder (i.e. the correct GeoJSON file at `Resources/obstacles.geojson` within the current `bin/...` folder)
2. Make sure the correct waypoints are in the resources folder (i.e. the correct GeoJSON file at `Resources/waypoints.geojson` within the current `bin/...` folder)
3. `sudo dotnet HikerModel.dll`

# Visualize the results

The `visualizations/` folder contains several python sripts generating visualizations via the python library seaborn.
See according README.md there.
