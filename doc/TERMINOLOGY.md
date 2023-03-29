# Terminology

## Neighbor

The term "neighbor" has two slightly different meanings:

1. **Visibility neighbor:** These neighbors are all vertices, regardless of the geometry they belong to, that are visible from a given vertex.
2. **Obstacle neighbors:** These are neighbors on geometries the given vertex belongs to. There can be more than one geometry the vertex belongs to, because geometries can share the same point (for example touching building).

## Vertex

Like in graph theory, a vertex is a point in space at a certain coordinate.

A vertex here has an additional interesting property: A list of neighboring vertices that are on the same or adjacent geometries (s. [DESIGN_DECISIONS.md](./DESIGN_DECISIONS.md) for further details).

## Obstacle

An obstacle is a line string or polygon and agents are not allowed to travel through them, so the routing algorithm tries to find a way around them.

## Wavefront/Wavelet

A wavelet (sometimes also called wavefront, but wavelet is the term used in papers) can be seen as an expanding arc or circle. The center of the arc is called root vertex. When looking from the root vertex, the left and right corners of the arc are at a certain angle, the so called angle area. The left corner is the from-angle and the right the to-angle. Next to the angles, a wavelet also has a distance (radius) from its root vertex.
