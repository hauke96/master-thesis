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
* When connecting locations to graph: Interprete roads as obstacles (so that shadow area optimization is used) but to not use the convex hull optimization. This should speed up routing queries since the majority of time goes into the connection step.
	* Also: Currently the agent might walk across all roads directly to the target, if there are no obstacles. This is probably not wantes, even though its technically right (because there are no obstacles)
* Connecting holes in Polygons which are in deed connected (e.g. an island in a lake connected with a bridge modeled as single way with bridge=yes).
* Remove unimportant nodes and edges as described in e.g. "A Modular Routing Graph Generation Method for Pedestrian Simulation" 2016 by Kielar
* Use different Kd-tree implemnentation
	* MARS implementation: Uses a stack, adding stuff is expensive due to "Array.Resize" operations. During routing (osm-based-rural/3km2) 29% of the time went into this single operation. During graph generation 18%.
	* NTS implementation:
		* 1. Needs a class but I have NodeData (struct) -> wrapper class needed -> no problem
		* 2. Doesn't support adding data to the exact same location (which i need due to the splitting of nodes for their angle areas): "When an inserted point is snapped to a (existing) node then a new node is not created but the count of the existing node is incremented.". Therefore, the "Query()" method just returns one node, not a list. -> NTS implementation not usable
	* Map based approach: Define accuracy/tolerance -> round every coordinate to that accuracy -> store in map and do queries accordingly
* Maybe directly splitting vertices by their valid angle area (and then determining KNN within the vertex's angle area) makes the implementation simpler
* Convex hull-filtering: Not working for polygons with vertices that are hidden to the outside (snail-like polygons for example; s. wekan)
