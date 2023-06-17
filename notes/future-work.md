Here are some thoughts on future work:

* Convex hull filtering: Suboptimal for non-shortest (e.g. fastest or otherwise weighted) routes. Solutions:
    * Deactivate filtering. No simple method for non-shortest path weighting is known to me, which results in a simple filtering mechanism.
* 3D data or at least "level" of OSM data
* Self-intersecting polygons (triangulation fails because convex hull cannot be determined correctly or something)
* Persist graph to not import it everytime
* Adding attributes to visibility edges (e.g. "surface=grass" when traversing grass area)
* Better routing profiles for more realistic routes
* Not many visibility edges → Roads are not well connected → Maybe explicitly connecting road vertices or at least junctions might help (but increases the graph size)
* Enhance performance of adding source/destination locations to graph as it requires most of the time during routing.
