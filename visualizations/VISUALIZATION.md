# Best practices

1. Make sure the QGIS projects follow certain properties:
	1. Use the `.qgs` instead of `.qgz` files as they are uncompressed XML and therefore at least somehow compatible with git
	2. Use relative paths and store all data files next to the `.qgs` project file
2. Compress TIF files. No really, don't skip this step, it can compress a 50MB TIF file down to 50K!
	1. Rename your original file or make a copy
	2. Use GDAL to compress the file: `gdal_translate -ot Byte -co BIGTIFF -co COMPRESS=ZSTD -co TILED=YES original.tif target.tif` You can also use `COMPRESS=JPEG` for even smaller files but with lossy compression.
	3. Of course, only check in the smaller `target.tif` file in git

# Isochrones via QGIS

1. Optional: Add a base map so you can see where you are (e.g. OpenStreetMap)
2. Import GeoJSON file as vector layer (you should see points on the map)
3. Select the vector layer
4. Open the toolbox (probably Ctrl+Alt+T)
5. Search for "Interpolation" and double click on "TIN-Interpolation"
6. Calculate interpolation layer:
	1. Select your vector layer
	2. Select the attribute on which to interpolate. For the points of the routes it's "time"
	3. Click on the "+" symbol to add the interpolation attribute to the list
	4. Select the extent of the data where you want to interpolate (probably want to interpolate for the whole dataset, so choose the extent of the layer via the menu behind the little drop-down arrow)
	5. Select the number of pixels per row and column
	6. Click "Start"
	7. A new grayscale raster-layer should appear
7. Adjust styling of raster layer:
	1. Open styling pane (probably via F7)
	2. Choose "Singleband pseudocolor" instead of grayscale style
	3. Choose "Discrete" as color interpolation type
	4. Choose a color ramp of your choice
	5. Choose "Equal interval" instead of "Continuous" as mode for the interpolation
	6. Choose the desired number of classes


