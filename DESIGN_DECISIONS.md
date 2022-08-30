# Design decisions

## General

* Distance: I use the euclidean distance since it's a lot faster to compute than the Haversine distance. The inaccuracy is negligible since distances are usually small (< 100m).

## Entities: Wavelet, vertices and obstacles

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

* Relevant vertices: Linked list instead of a queue/heap:
	1. When a wavefront is split into two, we can just use the existing relevant vertices of the old wavefront and just filter them by their angle. This is way faster than creating and filling a new priority queue or heap.
	2. Removing the min element in a min-heap (at least in the `FibonacciHeap` implementation of the MARS framework) is in O(log n) amortized. Getting and removing the first element in a linked list is both in O(1).

### Routing algorithm

#### Wavelets: Heap instead of list

**What the algorithm does:** Increase the distance of a wavelet when the nearest vertex is being processed → The wavelet moves further back in the sorted data structure.

**Current implementation:** Use a Fibonacci heap. Whenever a re-sort is needed for a wavelet, it'll be removed from the heap and re-added with the updated key.

1. Inserting happens quite often and uses just O(1) in a heap instead of up to O(n + log n) in a sorted list (either O(log n) search + O(n) insert in a List or O(n) search and O(1) insert in LinkedList)
2. Removal happens slightly less often and takes just O(log n) even though a linked list would be faster with O(1)

**Alternatives:** A list based structure (array based `List` or `LinkedList`) with slower performance as shown above and below.

I also tried to implement a sorted linked list with an bin based index on top (s. branch `custom-sorted-linked-list`).
The index just took the key of an element (in this case the wavelets distance from source), did some rounding of that number make the bin larger.
A bin then just pointed to the first `LinkedListNode` of that bin in the list.
The performance was okay but didn't improve the overall performance.
Also the rounding of the key wasn't working to well and a lot of bins were empty while others were quite full.

**Conclusion:** Insert + remove take about O(log n) using the Fibonacci heap and at least O(n) in a list

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

**Current implementation:** A list of shadow areas (s. [Algorithm descriptions](ALGORITHMS.md) for further details) is maintained and used to check if a single vertex is within one of these areas and therefore not visible to a root vertex.

**Alternatives:**
* Just rely on normal geometric collision checks. This is already done, when no shadow area was found but such collision checks are always rather slow compared to simple boolean expressions for a shadow area check.
* Previous implementations checked the whole area of an obstacle for within-relation (so full intersection) with a shadow area. But since the `BinIndex` class is used (s. below), only point-checks are used to see whether a single point is within a shadow area.

## Algorithms

For full details on the routing, see the [ALGORITHMS.md](./ALGORITHMS.md) file.

### Routing: Handling neighbors

#### Motivation: Why handling neighbors?

When a wavelet reaches a vertex, several things might happen:

* The wavelet dies because of several reasons:
	* There are no further vertices in the angle area of the wavelet
	* The wavelet reached the inside corner of a line/polygon and therefore cannot continue
* A new wavelet will be created when there's a shadow cast by the obstacle
* The wavelets angle area will be reduces because parts of it are within a shadow and therefore irrelevant for the current wavelet
* The wavelet will be split into two because the obstacle is right in the middle of the wavelets angle area

Also some combinations of the above can happen, e.g. the wavelet might cast a shadow, so its angle area will be adjusted, and a new wavelet will be spawned covering the shadow area.

#### Data structure

Prior to the current implementation, a vertex possibly had a left and a right neighbor.

The current implementation allows an arbitrary amount of neighbors but it's still possible to get the left and right neighbors seen from a given reference angle (e.g. a vertex has neighbors at 10°, 20° and 30°, so the right and left neighbors relative to 15° would be 10° and 20°).

A special case is a vertex with only one neighbor, however, the current implementation doesn't care and returns the same neighbor vertex for left and right. This is also the desired behavior for later processing of these neighbors.

#### Rotation of angles

When handling neighbors of a vertex `v`, the first thing that's done is to calculate all relevant angles. The angle between to root of the wavelet and the neighbors but also the angles between `v` and its neighbors.

In a second step, all those angles are rotated so that the angle `a` between the wavelets root vertex and `v` is 0°. So every angle is rotated by `-a°`.

#### Handling shadows and adjust wavelet

Thanks to the rotation, it's very easy to check whether a shadow is cast by the wavelet or not: When left and right neighbors are on the same side (between 0° and 180° or 180° and 360°), a shadow is cast and a new wavelet needs to be created.

The angle area of the existing wavelet may have to be adjusted. This only happens when 1.) a shadow is cast and 2.) the neighbor, where the shadow is cast, has been visited before. The second condition is important to not adjust the wavelet too early, because in the "triangle" between wavelet, current vertex and neighbor might exist further vertices. Those vertices might be skipped when the wavelet is adjusted too early.

## Optimizations

### Preprocessing

#### Detecting intersections

**What the algorithm does:** It wants to check whether to lines (not line strings, just two lines a→b and c→d) intersect.

**Current implementation:** Own and slightly optimized implementation of the line collision algorithm described by Cormen 3rd edition chapter 33.1.

**Alternatives** and how they perform compared to the current implementation:

1. `Geometry.Intersect`: About 70 times slower (not tested if results are correct)
2. `IPreparedGeometry.Intersect`: About 9 times slower (not tested if results are correct)
3. `IntersectionComputer.Intersection`: About 3 times slower (+ not the same functionality → unit tests fail)

#### Shadow areas

##### Index

###### CITree - A 1D quadtree for intervals

Storing and querying shadow areas is a challenge, because the 360° == 0° equality and the fast of different distances makes it harder than standard interval problems. Also the data structure should only return full overlaps, not only intersections. Nevertheless, the NetTopologySuite has a `Bintree` implementation, which is a 1D quadtree and can be used for intervals.

However, I extended the `Bintree` class to create my own `CITree` (cyclic intervall tree) implementation. To make it work with angles, I needed own `Add` and `Query` methods. Unfortunately this implementation was not sufficiently fast and this had several reasons:

1. Queries overlapping the 360°/0° border had to be cut into two queries → more overhead
2. Same for insertions even though inserting is faster
3. Query results had to be filtered because 1.) the quadtree result only contains "candidate items which may overlap the query interval" (quote of doc.) and 2.) to get entries which are entirely within the given query interval
4. Query results needed to be unique, so a `Distinct()` call for separate queries (s. above) were necessary

All in all the performance - even for point queries - was worse than just using a list of intervals.

###### BinIndex - A bin based index for intervals

The current implementation uses the `BinIndex` which is a bin based index. Each bin contains several interval objects and queries quite fast because the underlying data structure is an array. The bin size is by default 1, so each bin covers a 1° area.

Because the current algorithm uses this index to check whether a single vertex is within any area, the query method just takes an angle and returns a collection of shadow areas. The query method then calculates the bin of the given angle and returns the content of this bin.

Using the mid sized Stellingen OSM dataset, all 4.7 mio. calls of the `Query` method combined just need 60ms and all 230k `Add` calls about 400-500ms.

##### Merging

I tried three times to merge shadow areas.
The idea is quite straight forward: If I have two overlapping shadow areas, I can merge them into one big area and therefore need less checks.

###### General problem with merging shadow areas

This whole situation is a (slightly more complex) interval problem.
First, we're dealing with angles here and an area of 350° to 10° is not the same as an area from 10° to 350°.
Second, there's a distance to consider.

If two areas are merged, the further distance of those has to be used for the new area, otherwise results might not be correct anymore.
This, however, reduces the efficiency as vertices that were in the nearer shadow area are not necessarily within the new and further shadow area. Example: Area `a` is from 10° to 20° and 3m away, area `b` from 20° to 30° and 10m away. The merged area is then from 10° to 30° and 10m away. A vertex at 15° and 8m away was hidden by `a` but is not anymore in the new merged area, therefore an expensive collision check will be performed.

###### Implementations

None of my following merging strategies worked.
Either there were too many difficulties in the implementation to work on that approach any further, or the performance was worse than before.

1. Whenever a new shadow area was created, try to merge it with others
2. Create a 1D R-Tree to store the shadow area intervals. Whenever an area exceeds the 360°/0° border, split it into two. Whenever a new shadow area was created, query for existing ones and merge all of them.
3. A relaxation approach: Whenever a merge happened, check for further merge opportunities. Repeat this until no further merges are possible.

### Ideas for further optimizations

#### Routing strategies

**Bidirectional routing:**
Start two wavelets from source and target node and stop routing when the two routing routines (or however this may be implemented) meet each other at the same node.

**Selection of next vertex:**
The current implementation sorts the wavelets by their distance to the next vertex. Then the wavelet at the front of the list is chosen (it has the lowest distance to the next vertex). One idea is to sort the wavelets by their distance to the target. This reduces the amount of wavelets moving in the opposite direction. But when to stop? When a wavelet reaches the target, it's not necessarily the shortest route that has been found.

**Line between source and target:**
Create a line between source and target. Spawn several wavelets along vertices next to this line. This reduces the amount of wavefront moving away from source and target vertex. Start routing and connect the separate ways that are found. Does this work, is the resulting route the shortest? 

#### Data structures

**X-Fast trie / Y-Fast trie:**
Such data structure could be used to store and access the wavelets. However only the smallest wavelet needs to be accessed, so complex queries are no use case here. Also, storing wavelets is not a performance issue.

**KNN-Graph:**
This could be used to store vertices and retrieve the nearest neighbors. I tried this already but the problem is: We don't want to know the nearest neighbors but the nearest *visible* neighbors. So the KNN-Graph would have to store a lot more than *K* neighbors so that later the *K* nearest visible neighbors can be found. But maybe it works quite well to set *K = 1000* (or 10.000?), because maybe the 100 nearest visible neighbors are all within the 1000 (or 10.000) general nearest neighbors. It's not perfect but might help.

**Faster index for obstacles:**
Currently the MARS `QuadTree` is used. I tried using the NetTopologySuite `STRtree` but without success. First, I have to implement my own visitor class to not collect everything as an array list (s. below). Second, even though less objects were returned (about half as much as when using the `QuadTree`) and less intersection checks needed to be made (about four times less) it was slower. Maybe the `STRtree` is generally slower than the `QuadTree`?

#### Algorithms

**Spatial pruning:**
This could be helpful for preprocessing when the nearest neighbors need to be found. Use several growing queries to get near vertices. If the first queries didn't contain enough nearest visible vertices, increase the query range. This can be done by rectangular queries so that no area is queried twice.

#### Miscellaneous

**Other programming language:**
I don't have the impression that C# (including the libraries) is meant to be used for high performance applications.

Example:
the NetTopologySuite `STRtree` class uses by default the normal array based `List` class to collect the resulting features [0]. Due to the resize of the array, a lot of data has to be copied over and over again decreasing the performance significantly.

[0]
https://github.com/NetTopologySuite/NetTopologySuite/blob/c5be50bb638be90ef8eb8c63456ae393194beba2/src/NetTopologySuite/Index/Strtree/AbstractSTRtree.cs#L279

**Parallelism:**
One could (hopefully) easily parallel the preprocessing by dividing the vertices in bins, every thread then works on one bin.

**Hershberger & Suri Algorithmus:**
This is also a wave propagation algorithm which is theoretically faster than Mitchells algorithm I'm using right now. However it's a 50+ page paper and sounds quite complicated. The question is: Is it worth the work? My thesis is not about implementing a very performant routing engine but rather to enhance routing results for agent based simulations.
