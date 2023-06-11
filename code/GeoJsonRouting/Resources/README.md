This folder contains several GeoJSON files with different scenarios for testing and demonstration purposes.
The algorithm reads the `obstacles.geojson` file in this folder and uses its geometries as obstacles.

# Use a GeoJSON file

1. Copy the desired file to this `Resources` folder
2. Name it `obstacles.geojson`

Done.

# OSM exports

The `osm-exports` folder contains real OpenStreetMap exports, see the license file there for attribution.

## New export via overpass
1. Go to https://overpass-turbo.eu
2. Paste your query (see examples below)
3. Press "Run"
4. Click "Export" and download the result as GeoJSON
5. Open the file in an editor of your choice (QGIS, JOSM, ...)
6. Add start node with the attribute `start=...` (the value is irrelevant, the key will be used in the import of this file)
7. Do the same for the destination with `destination=...`
8. Save the file somewhere here and you're done

### Query examples

Get buildings and barriers (fence, wall, etc.) within the current visible map:

```
[out:json][timeout:25];
(
  way["building"]({{bbox}});
  way["barrier"]({{bbox}});
);
out body;
>;
out skel qt;
```

Get buildings and barriers in Hamburg, Germany:

```
[out:json][timeout:2500];
{{geocodeArea:Hamburg}}->.searchArea;
(
  way["building"](area.searchArea);
  way["barrier"](area.searchArea);
);
out body;
>;
out skel qt;
```
