# Generate pattern based datasets

The folders `pattern-based-...` each contain a `pattern.geojson` which can be seamlessly repeated to form a bigger dataset.

These datasets can be created with the following command using the `DatasetCreator` project.
Execute the `DatasetCreator.dll` (or start the project from the IDE) without parameters to get usage information.

This tool takes an area and the amount of pattern in x and y direction and the file `pattern.geojson` as input.
The pattern is then scaled and repeated to fit exactly within the given area.
Coordinates will be snapped to each other (which connects near line strings) and coordinates near a line will be snapped to the closest point on that line (again connecting line strings).

Example usage:
```sh
dotnet ../path/to/DatasetCreator 0 0 10 10 3 3 false pattern.geojson
```

This example produces the 3x3 dataset within the extent coordinates of (0, 0) and (10, 10) without snapping the geometries (→ `false` parameter before file name).
Use other values than "3 3" to create datasets with more or less repititions.

To create multiple datasets at once, use this:

```sh
V="1 2 4 6 8 10 12 14 16 18 20 22 24 26 28 30"
for v in $V; do dotnet ../../../code/DatasetCreator/bin/Release/net7.0/DatasetCreator.dll 0 0 10 100 $v $v true pattern.geojson; done
```
