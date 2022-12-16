# Performance evaluation

## Pure geometric routing performance

As of: 2022-11-22 (commit 4ed6cbc3b1c2dbefc50735cca83ba24a53b0b26f).

### Theoretic considerations

The optimum would be a Big-O notation of the algorithm.
A separate view on preprocessing and routing would be good.

Use these considerations kind of as hypotheses for the experiments below.

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

## OSM datasets

Requirements:

* Installed QGIS
* The NPM tool [@mapbox/geojson-merge](https://www.npmjs.com/package/@mapbox/geojson-merge)

Steps to create the finally used OSM dataset using QGIS and an external NPM tool (s. above).
One could use python and GDAL to accomplish this but I'm not familiar with the bare GDAL API, but QGIS is a tool I frequently use.

**Tip:**
The QGIS tools always create new layers and I recommend to directly remove the old layers.
This ensures that you don't become confused with all the different layers.
Example: You reproject a layer "my layer" and get a layer "reprojected", then directly remove the layer "my layer".

1. Use Overpass Turbo to create an export with relevant obstacles (e.g. buildings and water areas): https://overpass-turbo.eu/s/1odg
2. Use Overpass Turbo to get passable areas which should be subtracted from the obstacles: https://overpass-turbo.eu/s/1odf
3. Postprocessing of obstacle data with JOSM
	1. Remove unwanted parts e.g. outside the city area you want to look at (in my case outside the inner city of Hamburg)
4. Postprocessing with QGIS:
	1. Load the obstacle data into QGIS
	2. Load passable area data into QGIS (to cut obstacles where e.g. roards are -> where pedestrian can walk)
	3. Use the "Reproject layer" tool to change the projection of the passable areas line layer to UTM32N (to use meters as unit in the next step)
	4. Use the "Buffer" tool to turn lines and points into polygons. Use e.g. 2m as buffer size (that's why the reprojection is useful). Use the "Square" cap and "Miter" join style for less vertices.
	5. Use the "Multipart to Singlepart" tool to convert the passable area layers (the buffered point and line layer as well as the polygon layer). This turns all multipolygons into normal polygons for the next step.
	6. Use the "Merge" tool to merge the three single part passable areas layers into one layer
	7. Use the "Difference" tool to subtract the merged passable area layer from the obstacle line and polygon layers (you might have to use the "Check validity" tool first to fix somehow broken geometries on the passable area layer)
	8. Use the "Dissolve" too to merge touching polygons into one.
	9. Use the "Delete holes" tool to remove unreachable holes in polygons.
	10. Export the two new obstacle layers to two GeoJSON files. QGIS cannot merge layers of different geometries, so we have to merge the two layers with an external tool.
	11. Use the @mapbox/geojson-merge tool to merge the two GeoJSON files into one (usage: `geojson-merge file.geojson otherfile.geojson > combined.geojson`)

### Problems

#### Level and layer attributes

It's not easy (or nearly impossible) to evaluate the level and layer attributes correctly.

**Example A:**
At Rödingsmarkt the U3 is on a bridge-polygon with no level- but a layer-tag (so interpreted as level=0;https://www.openstreetmap.org/way/953758495). It also has no railway-tag. Ignoring such constellations (bridge with layer=1) would, however, ignore wanted bridges like the Slamatjenbrücke (https://www.openstreetmap.org/way/368522520).

**Example B:**
Ignoring highways with values of "level" or "layer" greater than 0, would ignore roads like parts of the Ludwig-Erhard-Straße (https://www.openstreetmap.org/way/164239554).

**My solution:**
For simplicity I ignore all negative layer values (to not cover subway ways etc.) but therefore tolerate errors on above-ground ways and bridges.

Another problem is that highways with different level attributes interfere with each other. For example you can leave the Elphi-Plaza via the underground parking exit. That doesn't make any sense but it's hard to avoid.

#### Semicolon separated lists

Are not covered but would just affect tag evaluation in Overpass making it more complex and slow.

## Artificial pattern datasets

There's the `DatasetCreator` project. It takes an area (CLI arguments), the amount of pattern in x and y direction (two last CLI parameters) and the file `pattern.wkt` as input. The pattern is then scaled and repeated to fit exactly within the given area.

# Run the evaluation

To run the evaluation, I used the CLI to be able to run the whole process using `sudo`. This allows the model to set the thread priority to "high" so that the evaluation process runs more or less on its own thread.

**Preparations:**

1. Go into the `HikerModel` folder (or whatever model you want to use)
2. `dotnet build --configuration Release `

**Using script:**

The Script `./evaluation/execute.sh` accepts the model path and parameter for the models themselves (see `-h` parameter for more information).
Running it like this uses the three smallest datasets from the pattern-datasets:

`evaluation/execute.sh code/HikerModel/bin/Release/net7.0/HikerModel.dll evaluation/datasets/pattern-based/ "1x1 2x2 3x3"`

Note: You must use `sudo` on Linux to change the thread priority to "high".

**Manual execution:**

4. Go into `bin/Release/net7.0/` of your model (or instead of `Release` and `net7.0` whatever configuration and .NET version you used)
5. Make sure the correct dataset is in the correct location (e.g. the correct GeoJSON file at `Resources/obstacles.geojson` for the HikingModel)
6. `sudo dotnet HikerModel.dll`

