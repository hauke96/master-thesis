The script `linestring-distance.py` does the following

1. Ensure every linestring has `n` coordinates (e.g. 100)
2. Group them by the `id` property
3. When a certain ID has exactly two linestrings: Calculate the Hausdorff distance

In order to make this work for the expected linestrings and the Hiker trip, some pre-processing has to be done on the trips file:

1. Split the huge trip linestring into the segments between each waypoints
2. Assign an `id` attribute to the segments (must be the same ID as in the expected linestring)
3. Make sure - in case a file from the QGIS GeoJSON files is used - only one linestring per ID per GeoJSON-file exists
4. Simplify the geometries
  * `ogr2ogr osm-city-hiker-simplified.geojson osm-city-hiker.geojson -simplify 0.00002`

Then use the script:

```bash
./linestring-distance.py osm-city-expected.geojson osm-city-hiker-simplified.geojson 2>/dev/null > osm-city-hausdorff_distances.csv
```
