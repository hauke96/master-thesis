# Create an OSM based dataset

**Requirements:**

* Installed QGIS
* The NPM tool [@mapbox/geojson-merge](https://www.npmjs.com/package/@mapbox/geojson-merge)

Steps to create the finally used OSM dataset using QGIS and an external NPM tool (s. above).
One could use python and GDAL to accomplish this but I'm not familiar with the bare GDAL API, but QGIS is a tool I frequently use.

**Tip:**
The QGIS tools always create new layers and I recommend to directly remove the old layers.
This ensures that you don't become confused with all the different layers.
Example: You reproject a layer "my layer" and get a layer "reprojected", then directly remove the layer "my layer".

**Steps:**

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

## Problems

### Level and layer attributes

It's not easy (or nearly impossible) to evaluate the level and layer attributes correctly.

**Example A:**
At Rödingsmarkt the U3 is on a bridge-polygon with no level- but a layer-tag (so interpreted as level=0;https://www.openstreetmap.org/way/953758495). It also has no railway-tag. Ignoring such constellations (bridge with layer=1) would, however, ignore wanted bridges like the Slamatjenbrücke (https://www.openstreetmap.org/way/368522520).

**Example B:**
Ignoring highways with values of "level" or "layer" greater than 0, would ignore roads like parts of the Ludwig-Erhard-Straße (https://www.openstreetmap.org/way/164239554).

**My solution:**
For simplicity I ignore all negative layer values (to not cover subway ways etc.) but therefore tolerate errors on above-ground ways and bridges.

Another problem is that highways with different level attributes interfere with each other. For example you can leave the Elphi-Plaza via the underground parking exit. That doesn't make any sense but it's hard to avoid.

### Semicolon separated lists

Are not covered but would just affect tag evaluation in Overpass making it more complex and slow.

# Simplify geometries

I used `ogr2ogr` to do that.
Here are the commands and remaining coordinates in the simplified datasets (datasets as of 2022-12-16):

* `ogr2ogr obstacles-final-simplified-1.geojson obstacles-final.geojson -simplify 0.0000001` → 17229
* `ogr2ogr obstacles-final-simplified-2.geojson obstacles-final.geojson -simplify 0.000001` → 15243
* `ogr2ogr obstacles-final-simplified-3.geojson obstacles-final.geojson -simplify 0.00001` → 11077
* `ogr2ogr obstacles-final-simplified-4.geojson obstacles-final.geojson -simplify 0.000015` → 9859
* `ogr2ogr obstacles-final-simplified-5.geojson obstacles-final.geojson -simplify 0.00003` → 7676
* `ogr2ogr obstacles-final-simplified-6.geojson obstacles-final.geojson -simplify 0.0001` → 5278

Note that these numbers differ from the really used number of vertices due to preprocessing steps and splitting of large obstacles.
