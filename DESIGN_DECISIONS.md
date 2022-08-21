# Design decisions

## General

* Distance: I use the euclidean distance since it's a lot faster to compute than the haversine distance. The inaccuracy is negligible since distances are usually small (< 100m).

## Objects: Wavelet, vertices and obstacles

### Vertex

I added my own vertex class to store certain important features:

* `Position` and `Coordinate` where the coordinate is pre-computed for faster access
* Positions of neighboring vertices

### Wavelet design

Each wavelet has...

* a from- and to-angle and a distance from the root (source/start or routing)
* the root vertex where this wavelet started
* a sorted list of all relevant vertices
* a list of visited vertices

A **relevant vertex** is a vertex which is in the angle range and visible for a wavelet.

### Ignoring Events

Wavelet-wavelet and wavelet-edge collisions are ignored (including any bisector curves).

**Reasons**:

* Reducing computational and implementation complexity
* When a wavelet-vertex collision happens, the wavelet is by strategy the one with the lowest distance from the root.

## Data structures

### Wavefront

* Relevant vertices: LinkedList instead of a queue/heap:
	1. When a wavefront is split into two, we can just use the existing relevant vertices of the old wavefront and just filter them by their angle. This is way faster than creating and filling a new priority queue or heap.
	2. Removing the min element in a min-heap (at least in the `FibonacciHeap` implementation of the MARS framework) is in O(log n) amortized. Getting and removing the first element in a linked list is both in O(1).

### Routing algorithm

#### Wavelets: Heap instead of list

**What the algorithm does:** Increase the distance of a wavelet when the nearest vertex is being processe → The wavelet moves further back in the sorted data structure.

**Current implementation:** Use a fibonacci heap. Whenever a re-sort is needed for a wavelet, it'll be removed from the heap and re-added with the updated key.

1. Inserting happens quite often and uses just O(1) in a heap instead of up to O(n + log n) in a sorted list (either O(log n) search + O(n) insert in a List or O(n) search and O(1) insert in LinkedList)
2. Removal happens slightly less often and takes just O(log n) even though a linked list would be faster with O(1)

**Alternatives:** A list based structure (array based `List` or `LinkedList`) with slower performance as shown above and below.

**Conclusion:** Insert + remove take about O(log n) using the fibonacci heap and at least O(n) in a list

#### Obstacles

Are passed to the preprocessor → s. below under "Visibility check for vertices".

### Preprocessing

#### Visibility check for vertices

**What the algorithm does:** It wants to know whether there's an obstacle between to points `a` and `b`. Therefore we have to query the features between to points and check for visibility.

**Current implementation:** A polygon storing `QuadTree` gives us all potentially intersecting obstacles in the envelope spanned by the two points. An `Action` handler then checks those polygons for intersection with the line `a → b`.

**Alternatives:** Other index structures like an R-Tree or plain lists.

**Reason for this approach:** Other indices (`STRtree` from NetTopologySuite and the MARS R-Tree implementation) haven't shown any advantages regarding performance. A list was fine (and fast) for a small amount of obstacles but obviously doesn't scale.

## Optimizations
