# Generate pattern based datasets

The folders `pattern-based-...` each contain a `pattern.geojson` which can be seamlessly repeated to form a bigger dataset.

These datasets can be created with the following command using the `DatasetCreator` project:

`dotnet ../path/to/DatasetCreator 0 0 10 10 3 3 false pattern.geojson`

This example produces the 3x3 dataset within the extent coordinates of (0, 0) and (10, 10) without snapping the geometries (→ `false` parameter before file name).
Use other values than "3 3" to create datasets with more or less repititions.
