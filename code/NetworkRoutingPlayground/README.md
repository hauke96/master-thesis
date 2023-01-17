# NetworkRoutingPlayground

This is just a playground project to see how the routing mechanisms of MARS work and to test how to add edges after loading existing network data.

## Prepare data

The GeoJSON file must fulfill one important property: The edges/ways in the file only reach from vertex to vertex.
A vertex in this case is a point with attributes.

How to produce such a file using an OSM dump:

1. Add an arbitrary attribute to all nodes in the dataset. This ensures that QGIS adds all nodes to a layer and not just the ones which have tags in OSM. 
2. Open the raw GeoJSON file in QGIS and only add points and lines. Polygons can probably also be processed but I focus here in points and lines.
3. Select the line layer and open to tool "Split with lines"
4. Make sure the line layer is selected twice, so it will intersect with itself.
5. Click "Run"
6. Now a new line layer should be on the map with line segments only reaching from one vertex to the next.
7. Export the point layer
8. Export the line layer to the same file (select "Append to layer" is QGIS notes that the file already exists)

You're done, the file can be used for routing.