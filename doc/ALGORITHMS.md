**Note:**
This file is out-of-date!

# Algorithms

## Preprocessing

Basically creates a KNN visibility graph, so it finds the k nearest visible vertices for each given vertex.

### Shadows

Here we have the concept of shadows.
A shadow is rooted in a vertex and has an angle area with a distance.
Everything within these angles and further away than the given distance is not visible from the root vertex (= in the shadow of an obstacle).

This is a method to simplify visibility checks because a shadow-check ("Is this vertex in one of the known shadow areas?") is very fast compared to a full collision check with a polygon.

### The algorithm

1. Loop over each vertex `v`
	1. Initialize list of visible neighbors, bin index of shadow areas and set of obstacles casting a shadow (kept to later check if an obstacle has been processed before)
	2. Sort all vertices by their distance to `v`. Potential optimization: Use index to get only a part of all vertices to reduce sorting costs.
	3. Loop over each sorted vertex `s` from near to far away
		1. Check if `s` is in a shadow area, if so, continue with next sorted vertex
		2. Get all obstacles in the envelope spanned by `v` and `s` and go through each obstacle `o`
			1. If the obstacle hasn't been visited, calculate and store its shadow area
			2. Do a visibility check by answering "Does the trajectory/line `(v, s)` intersect with `o`?"
		3. If all visibility checks where positive (= no obstacle is between `v` and `s`), add `s` to the list of visible neighbors

## Routing algorithm

1. Add start and target vertices and create wavelet at start vertex
2. Main loop `ProcessNextEvent`
	1. Get vertex `v` with the nearest distance to any wavelet `w`
	2. Check if vertex can be ignored and return if so
	3. Update `w` and the wavelet queue by removing `w` (may be re-added later)
	4. Handle neighbors of `v` (→ `HandleNeighbors`)
		1. Get right and left neighbor vertices of `v`
		2. Calculate all important angles (for example from the wavelet-root to the right neighbor)
		4. Rotate all angles so that the angle "`w` to `v`" is 0
		5. Calculate angle area of potentially new wavefront
		6. If a new wavefront should be created (angles != NaN && neighbors won't be visited by current wavelet), then do so
		7. Calculate and return angle area of shadow cast by current wavelet `w`
	5. Store predecessor relationship (predecessor of `v` is root of `w`)
	6. Split `w` if it's casting a shadow and insert potentially new wavelet(s) into wavelet queue
