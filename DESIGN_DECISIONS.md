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

I also tried to implement a sorted linked list with an bin based index on top (s. branch `custom-sorted-linked-list`).
The index just took the key of an element (in this case the wavelets distance from source), did some rounding of that number make the bin larger.
A bin then just pointed to the first `LinkedListNode` of that bin in the list.
The performance was okay but didn't improve the overall performane.
Also the rounding of the key wasn't working to well and a lot of bins were empty while others were quite full.

**Conclusion:** Insert + remove take about O(log n) using the fibonacci heap and at least O(n) in a list

#### Obstacles

Are passed to the preprocessor → s. below under "Visibility check for vertices".

### Preprocessing

#### Visibility check for vertices

**What the algorithm does:** It wants to know whether there's an obstacle between to points `a` and `b`. Therefore we have to query the features between to points and check for visibility.

**Current implementation:** A polygon storing `QuadTree` gives us all potentially intersecting obstacles in the envelope spanned by the two points. An `Action` handler then checks those polygons for intersection with the line `a → b`.

**Alternatives:** Other index structures like an R-Tree or plain lists.

**Reason for this approach:** Other indices (`STRtree` from NetTopologySuite and the MARS R-Tree implementation) haven't shown any advantages regarding performance. A list was fine (and fast) for a small amount of obstacles but obviously doesn't scale.

#### Shadow areas

**What the algorithm does:** It checks whether a vertex is visible or if it's hidden by an obstacle (= in the shadow of that obstacle).

**Current implementation:** A list of shadow areas (s. [Algorithm descriptions](ALGORITHMS.md) for further details) is maintained and used to check if a vertex is within one of these areas and therefore not visible to a root vertex.

**Alternatives:** Just rely on normal geometric collision checks. This is already done, when no shadow area was found but such collision checks are always rather slow compared to simple boolean expressions for a shadow area check.

## Optimizations

### Preprocessing

#### Collision detection

**What the algorithm does:** It wants to check whether to lines (not line strings, just two lines a→b and c→d) intersect.

**Current implementation:** Own and slightly optimized implementation of the line collision algorithm described by Cormen 3rd edition chapter 33.1.

**Alternatives** and how they perform compared to the current implementation:

1. `Geometry.Intersect`: About 18 times slower
2. `IPreparedGeometry.Intersect`: About 3 times slower
3. `IntersectionComputer.Intersection`: About 1.5 times slower

#### Shadow areas

##### Index

// TODO 1D R-Tree

##### Merging

I tried three times to merge shadow areas.
The idea is quite straight forward: If I have two overlapping shadow areas, I can merge them into one big area and therefore need less checks.

###### General problem with merging shadow areas

This whole situation is a (slightly more complex) interval problem.
First, we're dealing with angles here and an area of 350° to 10° is not the same as an area from 10° to 350°.
Second, there's a distance to consider.

If two areas are merged, the further distance of those has to be used for the new area, otherwise results might not be correct anymore.
This, however, reduces the efficiency as vertices that were in the nearer shadow area are not necessarrily within the new and further shadow area. Example: Area `a` is from 10° to 20° and 3m away, area `b` from 20° to 30° and 10m away. The merged area is then from 10° to 30° and 10m away. A vertex at 15° and 8m away was hidden by `a` but is not anymore in the new merged area, therefore an expensive collision check will be performed.

###### Implementations

None of my following merging strategies worked.
Either there were too many difficulties in the implementation to work on that approach any further, or the performance was worse than before.

1. Whenever a new shadow area was created, try to merge it with others
2. Create a 1D R-Tree to store the shadow area intervals. Whenever an area exceeds the 360°/0° border, split it into two. Whenever a new shadow area was created, query for existing ones and merge all of them.
3. A relaxation approach: Whenever a merge happened, check for further merge opportunities. Repeat this until no further merges are possible.
