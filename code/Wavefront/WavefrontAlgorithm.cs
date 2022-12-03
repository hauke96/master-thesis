using System.Collections.Immutable;
using System.Diagnostics;
using Mars.Common.Collections;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront
{
    public class WavefrontAlgorithm
    {
        // Maximum number of visible vertices considered to be neighbors. The term "neighbor" here is the general one
        // across all obstacles.
        private readonly int knnSearchNeighbors = 100;

        private readonly QuadTree<Obstacle> _obstacles;

        // Map from vertex to neighboring vertices. The term "neighbor" here refers to all vertices with an edge to the
        // key vertex of a dict entry.
        private Dictionary<Vertex, List<Vertex>> _vertexNeighbors;

        // Stores the predecessor of each visited vertex position. Recursively following the predecessors from a vertex
        // v to the source gives the shortest path from the source to v.
        public Dictionary<Waypoint, Waypoint?> WaypointToPredecessor;
        public Dictionary<Position, Waypoint> PositionToWaypoint;

        // Stores the known wavelet roots and their predecessor to make sure that wavelets are only spawned at vertices
        // where no other wavelet has been spawned yet. Following the predecessors from the target back to the source
        // yields the actual route the wavelets took and therefore the shortest path.
        public Dictionary<Waypoint, Waypoint?> WavefrontRootPredecessor;
        public Dictionary<Position, Waypoint> WavefrontRootToWaypoint;

        public FibonacciHeap<Wavelet, double> Wavefronts;
        public readonly List<Vertex> Vertices;

        public WavefrontAlgorithm(List<Obstacle> obstacles)
        {
            // Cut obstacles into line strings with a maximum length. This enhances the performance, because collision
            // checks are now performed on smaller objects.
            var maxObstacleLength = 50;
            obstacles = obstacles.Map(o =>
            {
                var result = new List<Obstacle>();

                if (o.Coordinates.Count <= maxObstacleLength)
                {
                    result.Add(o);
                    return result;
                }

                for (int i = 0; i < o.Coordinates.Count - 1; i += maxObstacleLength)
                {
                    if (i + maxObstacleLength < o.Coordinates.Count)
                    {
                        result.Add(new Obstacle(new LineString(o.Coordinates.Skip(i).Take(maxObstacleLength + 1)
                            .ToArray())));
                    }
                    else
                    {
                        result.Add(new Obstacle(new LineString(o.Coordinates.Skip(i).ToArray())));
                    }
                }

                return result;
            }).SelectMany(x => x).ToList();
            Log.D($"Amount of obstacles: {obstacles.Count}");

            _obstacles = new QuadTree<Obstacle>();
            obstacles.Each(obstacle => _obstacles.Insert(obstacle.Envelope, obstacle));

            Log.I("Get direct neighbors on each obstacle geometry");
            Dictionary<Position, List<Position>> positionToNeighbors = new();
            var result = PerformanceMeasurement.ForFunction(
                () => { positionToNeighbors = WavefrontPreprocessor.GetNeighborsFromObstacleVertices(obstacles); },
                "GetNeighborsFromObstacleVertices");
            result.Print();
            result.WriteToFile();

            Log.I("Create map of direct neighbor vertices on the obstacle geometries");
            Vertices = positionToNeighbors.Keys.Map(position => new Vertex(position, positionToNeighbors[position]));

            Log.I("Calculate KNN to get visible vertices");
            result = PerformanceMeasurement.ForFunction(() =>
            {
                _vertexNeighbors =
                    WavefrontPreprocessor.CalculateVisibleKnn(_obstacles, Vertices, knnSearchNeighbors);
            }, "CalculateVisibleKnn");
            result.Print();
            result.WriteToFile();

            Reset();
        }

        /// <summary>
        /// Clears the results of a previous routing run.
        /// </summary>
        private void Reset()
        {
            WaypointToPredecessor = new Dictionary<Waypoint, Waypoint?>();
            PositionToWaypoint = new Dictionary<Position, Waypoint>();
            WavefrontRootPredecessor = new Dictionary<Waypoint, Waypoint?>();
            WavefrontRootToWaypoint = new Dictionary<Position, Waypoint>();
            Wavefronts = new FibonacciHeap<Wavelet, double>(0);
        }

        /// <summary>
        /// Calculates the optimal geometric route from the source vertex to the target vertex.
        /// </summary>
        /// <returns>The optimal route as well as all other routes found during this method call. Finding these
        /// alternative routes stopped at the moment the target was reached.</returns>
        public RoutingResult Route(Position source, Position target)
        {
            var stopwatch = new Stopwatch();

            Reset();

            var sourceVertex = new Vertex(source);
            Vertices.Add(sourceVertex);

            var targetVertex = new Vertex(target);
            Vertices.Add(targetVertex);

            SetPredecessor(source, null, stopwatch, WaypointToPredecessor, PositionToWaypoint);
            SetPredecessor(source, null, stopwatch, WavefrontRootPredecessor, WavefrontRootToWaypoint);

            _vertexNeighbors[sourceVertex] =
                WavefrontPreprocessor.GetVisibleNeighborsForVertex(_obstacles, Vertices, sourceVertex,
                    knnSearchNeighbors);

            var neighborsOfTarget =
                WavefrontPreprocessor.GetVisibleNeighborsForVertex(_obstacles, Vertices, targetVertex,
                    knnSearchNeighbors);
            // TODO Find a better way to add the target to the existing neighbors lists?
            neighborsOfTarget.Each(neighbor => _vertexNeighbors[neighbor].Add(targetVertex));

            var initialWavefront = Wavelet.New(0, 360, sourceVertex, _vertexNeighbors[sourceVertex], 0, false);
            if (initialWavefront == null)
            {
                return new RoutingResult();
            }

            AddWavefront(initialWavefront);

            Log.Init(0);
            Log.I($"Routing from {source} to {target}");
            Log.D($"Initial wavelet at {initialWavefront.RootVertex.Position}");

            stopwatch.Start();
            while (!PositionToWaypoint.ContainsKey(target) && !Wavefronts.IsEmpty())
            {
                ProcessNextEvent(target, stopwatch);
            }

            // Clean up the list for future uses of the Route() method
            neighborsOfTarget.Each(neighbor => _vertexNeighbors[neighbor].Remove(targetVertex));

            List<Waypoint> waypoints = new List<Waypoint>();

            var targetWaypoints = WavefrontRootPredecessor.Keys.Where(k => k.Position.Equals(target)).ToList();
            if (!targetWaypoints.IsEmpty())
            {
                waypoints.AddRange(GetOptimalRoute(targetWaypoints.First(), WavefrontRootPredecessor));
            }

            return new RoutingResult(waypoints, GetAllRoutes());
        }

        private List<List<Waypoint>> GetAllRoutes()
        {
            var waypoints = WaypointToPredecessor.Keys.ToList();
            var predecessors = WaypointToPredecessor.Values.ToImmutableHashSet();

            // Find all positions which are *not* a predecessor of some other position. This means we found all leafs
            // of our predecessor tree.
            var leafWaypoints = waypoints.Where(p => !predecessors.Contains(p));

            return leafWaypoints.Map(w => GetOptimalRoute(w, WaypointToPredecessor));
        }

        private List<Waypoint> GetOptimalRoute(Waypoint start, Dictionary<Waypoint, Waypoint?> predecessorRelation)
        {
            var waypoints = new List<Waypoint>();
            var nextWaypoint = start;

            while (nextWaypoint != null)
            {
                waypoints.Add(nextWaypoint);
                nextWaypoint = predecessorRelation.ContainsKey(nextWaypoint)
                    ? predecessorRelation[nextWaypoint]
                    : null;
            }

            if (waypoints.Count == 1)
            {
                // Special case: No route found -> Return empty list instead of a list with just the given position in it.
                return new List<Waypoint>();
            }

            waypoints.Reverse();
            return waypoints;
        }

        /// <summary>
        /// Takes the wavelet with the smallest distance to the next vertex and processes that wavelet-vertex event.
        ///
        /// This method takes care of everything:
        /// It adjusts the existing wavelet if needed, creates new wavelets if necessary and stores the predecessor
        /// relation of the vertices.
        /// </summary>
        public void ProcessNextEvent(Position targetPosition, Stopwatch stopwatch)
        {
            var waveletNode = Wavefronts.Min();
            var wavelet = waveletNode.Data;
            var currentVertex = wavelet.GetNextVertex();

            if (currentVertex == null)
            {
                // This wavelet doesn't have any events ahead, to we can remove it. 
                Wavefronts.RemoveMin();
                return;
            }

            var currentVertexWasRootOfWaveletBefore = WavefrontRootToWaypoint.ContainsKey(currentVertex.Position);
            if (currentVertexWasRootOfWaveletBefore)
            {
                // The current vertes was already used as a root vertex of a wavelet. This means there are shorter paths
                // to this vertex and we can ignore it since the path using the current wavelet is not optimal.
                RemoveAndUpdateWavefront(waveletNode);
                AddWavefront(wavelet);
                return;
            }

            if (Equals(currentVertex.Position, targetPosition))
            {
                Log.I($"Target reached ({currentVertex.Position})", "", 1);
                SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch, WaypointToPredecessor,
                    PositionToWaypoint);
                SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch,
                    WavefrontRootPredecessor, WavefrontRootToWaypoint);
                RemoveAndUpdateWavefront(waveletNode);
                return;
            }

            // Updating the current wavelet means that the current vertex is marked as "done" by removing it from the
            // wavelets vertex queue. This can be done since that vertex is processed right now.
            // The wavelet is then removed from the list because of the following strategy: If the current wavelet
            // should be split or adjusted, it's easier to just create new wavelet(s) and add them. If nothing needs to
            // be done to the current wavelet, it'll just be re-added to the list.
            RemoveAndUpdateWavefront(waveletNode);

            // The wavelet hit the current vertex which is (probably) connected to other vertices and these
            // lines/polygons might cast shadows. These shadow areas are areas that the current wavelet doesn't need to
            // consider anymore. However, that's handled below, this code here just finds the shadow areas and spawns
            // new wavelets if needed (e.g. wavelets behind a corner).
            double angleShadowFrom;
            double angleShadowTo;
            HandleNeighbors(currentVertex, wavelet, out angleShadowFrom, out angleShadowTo,
                out var newWavefrontCreatedAtEventRoot);

            if (newWavefrontCreatedAtEventRoot)
            {
                // A new wavelet has been spawned with its root equal to the currently visited vertex. This means that
                // the current vertex is now the root of a wavelet for the first time. This is the case because
                // otherwise the current vertex would not have been considered in the first place (s. exit conditions
                // above).
                SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch,
                    WavefrontRootPredecessor, WavefrontRootToWaypoint);
            }

            // Save the normal vertex predecessor relation for the current vertex since it's the first visit of this
            // vertex (s. exit conditions above). 
            SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch, WaypointToPredecessor,
                PositionToWaypoint);

            // If the current wavelet actually casts a shadow, it can be adjusted. This is done by simply adding new 
            // wavelets with correct angle areas. The current wavelet (which is now more like an "old" wavelet), has
            // been already removed above.
            // If no shadow is cast, we simply re-add the current wavelet as it's still correct and valid.
            if (!Double.IsNaN(angleShadowFrom) && !Double.IsNaN(angleShadowTo))
            {
                // Determine which and of the current wavelet is within the shadow so that we can create new wavelets
                // accordingly.
                var fromAngleInShadowArea = Angle.IsBetween(angleShadowFrom, wavelet.FromAngle, angleShadowTo);
                var toAngleInShadowArea = Angle.IsBetween(angleShadowFrom, wavelet.ToAngle, angleShadowTo);

                if (fromAngleInShadowArea && toAngleInShadowArea)
                {
                    // Both ends of our wavelet are within the shadow area -> Just use the inverted shadow area as
                    // wavelet angle area. It cannot happen that the wavelets area is completely within the shadow area
                    // because that would mean this wavelet visited vertices outside its angle area and that's not
                    // possible. Therefore it's safe to use the inverted shadow area here.
                    AddNewWavefront(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        angleShadowTo, angleShadowFrom, true);
                }
                else if (fromAngleInShadowArea)
                {
                    // Only the from-angle of the current wavelet is within the shadow area, so we just create one new
                    // wavelet with adjusted from-angle.
                    AddNewWavefront(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        angleShadowTo, wavelet.ToAngle, true);
                }
                else if (toAngleInShadowArea)
                {
                    // Only the to-angle of the current wavelet is within the shadow area, so we just create one new
                    // wavelet with adjusted to-angle.
                    AddNewWavefront(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        wavelet.FromAngle, angleShadowFrom, true);
                }
                else
                {
                    // Shadow area is completely within the wavelets angle area -> Split out wavelet into two so that
                    // the area of the shadow is not covered anymore.
                    AddNewWavefront(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        wavelet.FromAngle, angleShadowFrom, true);
                    AddNewWavefront(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        angleShadowTo, wavelet.ToAngle, true);
                }
            }
            else
            {
                // Wavelet is not casting a shadow -> Re-add it because it's still valid.
                AddWavefront(wavelet);
            }
        }

        /// <summary>
        /// Stores the given predecessor position for the given vertex. This also stores metadata like the time this
        /// vertex has been visited and how many vertices had been visited before this one. 
        /// </summary>
        private void SetPredecessor(Position vertexPosition, Position? predecessorPosition, Stopwatch stopwatch,
            Dictionary<Waypoint, Waypoint?> waypointToPredecessor, Dictionary<Position, Waypoint> positionToWaypoint)
        {
            Waypoint? predecessor = null;
            if (predecessorPosition != null)
            {
                positionToWaypoint.TryGetValue(predecessorPosition, out predecessor);
            }

            Waypoint waypoint;
            if (!positionToWaypoint.ContainsKey(vertexPosition))
            {
                waypoint = new Waypoint(vertexPosition, positionToWaypoint.Count, stopwatch.Elapsed.TotalMilliseconds);
                waypointToPredecessor[waypoint] = predecessor;
                positionToWaypoint[vertexPosition] = waypoint;
            }
        }

        /// <summary>
        /// This method considers the given vertex to be visited by the given wavelet. This might cause a shadow casted
        /// by the neighbors of this vertex based on their visited status.
        /// </summary>
        /// <param name="currentVertex">The vertex visited by the given wavelet</param>
        /// <param name="wavelet">The wavelet visiting the given vertex</param>
        /// <param name="angleShadowFrom">The resulting from-angle of a potential shadow area, NaN if no shadow is cast.</param>
        /// <param name="angleShadowTo">The resulting to-angle of a potential shadow area, NaN if no shadow is cast.</param>
        /// <param name="createdWavefrontAtCurrentVertex">True if a new wavelet has been spawned with the root vertex equal to the given currentVertex.</param>
        public void HandleNeighbors(Vertex currentVertex, Wavelet wavelet, out double angleShadowFrom,
            out double angleShadowTo, out bool createdWavefrontAtCurrentVertex)
        {
            angleShadowFrom = Double.NaN;
            angleShadowTo = Double.NaN;
            createdWavefrontAtCurrentVertex = false;

            var angleRootToCurrentVertex = Angle.GetBearing(wavelet.RootVertex.Position, currentVertex.Position);
            var waveletAngleStartsAtVertex = Angle.AreEqual(wavelet.FromAngle, angleRootToCurrentVertex);
            var waveletAngleEndsAtVertex = Angle.AreEqual(wavelet.ToAngle, angleRootToCurrentVertex);

            var rightNeighbor =
                currentVertex.RightNeighbor(wavelet.RootVertex.Position, waveletAngleStartsAtVertex) ??
                currentVertex.LeftNeighbor(wavelet.RootVertex.Position, waveletAngleEndsAtVertex);
            var leftNeighbor = currentVertex.LeftNeighbor(wavelet.RootVertex.Position, waveletAngleEndsAtVertex) ??
                               currentVertex.RightNeighbor(wavelet.RootVertex.Position, waveletAngleStartsAtVertex);

            if (rightNeighbor == null && leftNeighbor == null)
            {
                return;
            }

            var angleRootToRightNeighbor = rightNeighbor != null
                ? Angle.GetBearing(wavelet.RootVertex.Position, rightNeighbor)
                : double.NaN;
            var angleRootToLeftNeighbor = leftNeighbor != null
                ? Angle.GetBearing(wavelet.RootVertex.Position, leftNeighbor)
                : double.NaN;

            var angleVertexToRightNeighbor = rightNeighbor != null
                ? Angle.GetBearing(currentVertex.Position, rightNeighbor)
                : double.NaN;
            var angleVertexToLeftNeighbor = leftNeighbor != null
                ? Angle.GetBearing(currentVertex.Position, leftNeighbor)
                : double.NaN;

            /*
             * Rotate such that the current vertex is always north/up of the wavelet root.
             * 
             * The idea behind it:
             * When the angles to the right and left neighbor are rotated we can easily check if there's a potential
             * shadow casted by the neighbors. When both neighbors are on the east side (0°-180°), we know that no other
             * neighbor is on the west side (180°-360°) and vice versa. This, however, means that there's an edge where
             * a potential shadow is cast.
             * If one neighbor is on the west and one on the east side, there's no such shadow.
             */
            var rotationAngle = -angleRootToCurrentVertex;
            var angleCurrentWavefrontFrom = Angle.Normalize(wavelet.FromAngle + rotationAngle);
            var angleCurrentWavefrontTo = Angle.Normalize(wavelet.ToAngle + rotationAngle);
            angleRootToRightNeighbor = Angle.Normalize(angleRootToRightNeighbor + rotationAngle);
            angleRootToLeftNeighbor = Angle.Normalize(angleRootToLeftNeighbor + rotationAngle);
            angleVertexToRightNeighbor = Angle.Normalize(angleVertexToRightNeighbor + rotationAngle);
            angleVertexToLeftNeighbor = Angle.Normalize(angleVertexToLeftNeighbor + rotationAngle);

            // Only consider edges to visited vertices to cast a shadow.
            var rightNeighborHasBeenVisited = wavelet.HasBeenVisited(rightNeighbor);
            var leftNeighborHasBeenVisited = wavelet.HasBeenVisited(leftNeighbor);

            // Both neighbors on west/east side of root+current vertex -> New wavelet needed for the shadow
            var bothNeighborsOnWestSide = Angle.GreaterEqual(angleVertexToRightNeighbor, 180) &&
                                          Angle.GreaterEqual(angleVertexToLeftNeighbor, 180);
            var bothNeighborsOnEastSide = Angle.LowerEqual(angleVertexToRightNeighbor, 180) &&
                                          Angle.LowerEqual(angleVertexToLeftNeighbor, 180);

            double angleNewWavefrontFrom = Double.NaN;
            double angleNewWavefrontTo = Double.NaN;

            /*
             * Only determine the angles of the new wavelet when
             *   a) both neighbors are on the west side
             *   AND
             *   b) the to-angle of the current wavelet is NOT 360°.
             * 
             * Condition b) might be fulfilled when condition a) holds and the wavelets to-side ends at an edge to ont
             * of the neighbors. This means that one of the neighbors is at 180° since this neighbor is either the root
             * vertex or a vertex placed exactly on the line between root vertex and neighbor. In this case, the wavelet
             * has reached an inner corner and no new wavelet is needed. Same holds when both neighbors are on the east
             * side and the wavelets from-side ends at 0°.
             * 
             * Here's a sketch where V (simple vertex) and R (root of wavelet) are both neighbors and both on the west.
             * Imagine the wavelet reached vertex X and has an angle-area of 270° to 360°. Now we know that the wavelet
             * reached an inner corner and no new wavelet is needed:
             * 
             *      V         V = Vertex (one of the neighbors)
             *       \
             *        X       X = Current vertex
             *        |
             *        |
             *        R       R = Root of wavelet and also a neighbors
             */
            if (bothNeighborsOnWestSide && !Angle.AreEqual(angleCurrentWavefrontTo, 360))
            {
                angleNewWavefrontFrom = Math.Max(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                angleNewWavefrontTo = 360;
            }
            else if (bothNeighborsOnEastSide && !Angle.AreEqual(angleCurrentWavefrontFrom, 0))
            {
                angleNewWavefrontFrom = 0;
                angleNewWavefrontTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
            }

            // Determine if the wavelets root vertex is the only neighbor. In other words see if we reached the end of a
            // line, because the last vertex of a line has only one neighbor (which means right = left neighbor). If
            // this neighbor is our wavelet root means that the wavelets root is the second last vertex of that line.
            var waveletRootIsSecondLastLineVertex = Equals(rightNeighbor, wavelet.RootVertex.Position) &&
                                                    Equals(leftNeighbor, wavelet.RootVertex.Position);

            // When the wavelet is rooted at the second last vertex of a line, then the last vertex of that line will
            // probably (!) be visited by this wavelet anyway, but only if the angle to that last vertex is exactly
            // the to- or from-angle of our wavelet. A wavelet could've been split before reaching the last vertex of
            // the line which means that there could be wavelets rooted at the second last vertex which will never reach
            // the last vertex of the line.
            // But when the angle to the right and left neighbor is the from/to angle of the wavelet, then both
            // neighbors will be visited by our wavelet. TODO rename to neighborsOnLineWillBeVisitedByWavefront
            var neighborsWillBeVisitedByWavefront = waveletRootIsSecondLastLineVertex &&
                                                    (
                                                        Angle.AreEqual(angleCurrentWavefrontFrom,
                                                            angleRootToRightNeighbor) ||
                                                        Angle.AreEqual(angleCurrentWavefrontTo,
                                                            angleRootToRightNeighbor)
                                                    ) &&
                                                    (
                                                        Angle.AreEqual(angleCurrentWavefrontFrom,
                                                            angleRootToLeftNeighbor) ||
                                                        Angle.AreEqual(angleCurrentWavefrontTo,
                                                            angleRootToLeftNeighbor)
                                                    );

            /*
             * When do we need a new wavelet? Two conditions must hold:
             *   1) We must've determined some potential from- and to-angles above, which means that there's a reason
             *      for a new wavelet in the first place.
             *   2) One of the neighbors will not be visited by the current wavelet. For example when the wavelet
             *      reached a corner, one neighbor is "behind" that corner and not visible -> new wavelet needed.
             */
            var newWavefrontNeeded = !double.IsNaN(angleNewWavefrontFrom) && !double.IsNaN(angleNewWavefrontTo) &&
                                     !neighborsWillBeVisitedByWavefront;

            // Rotate angles back to the actual values because now they are used to determine the return values.
            angleNewWavefrontFrom = Angle.Normalize(angleNewWavefrontFrom - rotationAngle);
            angleNewWavefrontTo = Angle.Normalize(angleNewWavefrontTo - rotationAngle);
            angleRootToRightNeighbor = Angle.Normalize(angleRootToRightNeighbor - rotationAngle);
            angleRootToLeftNeighbor = Angle.Normalize(angleRootToLeftNeighbor - rotationAngle);

            if (newWavefrontNeeded)
            {
                double distanceToRootFromSource = wavelet.DistanceTo(currentVertex.Position);
                createdWavefrontAtCurrentVertex = AddNewWavefront(_vertexNeighbors[currentVertex], currentVertex,
                    distanceToRootFromSource, angleNewWavefrontFrom, angleNewWavefrontTo, false);
            }

            // Now the shadow areas to the right and left neighbors will be calculated. Only visited neighbors are
            // considered here, because it can happen that geometries are within the shadow area but closer to the
            // wavelets root than the respective neighbor. In such a scenario a shadow between the wavelet root and an
            // UNvisited neighbor would exclude geometries from being visited at all.

            double angleRightShadowFrom = Double.NaN;
            double angleRightShadowTo = Double.NaN;
            if (rightNeighborHasBeenVisited)
            {
                Angle.GetEnclosingAngles(angleRootToRightNeighbor, angleRootToCurrentVertex, out angleRightShadowFrom,
                    out angleRightShadowTo);
                angleShadowFrom = angleRightShadowFrom;
                angleShadowTo = angleRightShadowTo;
            }

            double angleLeftShadowFrom = Double.NaN;
            double angleLeftShadowTo = Double.NaN;
            if (leftNeighborHasBeenVisited)
            {
                Angle.GetEnclosingAngles(angleRootToLeftNeighbor, angleRootToCurrentVertex, out angleLeftShadowFrom,
                    out angleLeftShadowTo);
                angleShadowFrom = angleLeftShadowFrom;
                angleShadowTo = angleLeftShadowTo;
            }

            // When two shadows exist -> merge them because they always touch since the current vertex is in the middle
            if (!Double.IsNaN(angleRightShadowFrom) && !Double.IsNaN(angleLeftShadowFrom))
            {
                if (Angle.AreEqual(angleRightShadowTo, angleLeftShadowFrom))
                {
                    angleShadowFrom = angleRightShadowFrom;
                    angleShadowTo = angleLeftShadowTo;
                }
                else
                {
                    angleShadowFrom = angleLeftShadowFrom;
                    angleShadowTo = angleRightShadowTo;
                }
            }
        }

        /// <summary>
        /// Adds a new wavelet for the given angles. If the angles exceed the 360°/0° border, then two wavelets will be
        /// created.
        /// </summary>
        /// <param name="relevantVertices">All vertices that should be considered. They will be filtered so that only
        /// relevant vertices are used in the wavelet.</param>
        /// <param name="rootVertex">The root vertex of the wavelet.</param>
        /// <param name="distanceToRootFromSource">The distance from the source to the given root vertex.</param>
        /// <param name="fromAngle">From angle of the new wavelet.</param>
        /// <param name="toAngle">To angle of the new wavelet</param>
        /// <param name="verticesFromWavefrontWithSameRoot">True when the given vertices come from a wavelet with the
        /// same root. This is a performance tweak to skip sorting the vertices.</param>
        /// <returns>True when wavelet was created and added, false otherwise.</returns>
        public bool AddNewWavefront(ICollection<Vertex> relevantVertices, Vertex rootVertex,
            double distanceToRootFromSource,
            double fromAngle, double toAngle, bool verticesFromWavefrontWithSameRoot)
        {
            bool newWavefrontCreated = false;

            toAngle = Angle.Normalize(toAngle);
            fromAngle = Angle.StrictNormalize(fromAngle);

            /*
             * If the interesting area exceeds the 0° border (e.g. goes from 300° via 0° to 40°), then we remove the
             * old wavelet and create two new ones. One from 300° to 360° and one from 0° to 40°. This simply
             * makes range checks easier and has no further reason.
             */
            if (Angle.IsBetweenWithNormalize(fromAngle, 0, toAngle))
            {
                newWavefrontCreated |= AddWavefrontIfValid(relevantVertices, rootVertex, distanceToRootFromSource,
                    fromAngle,
                    360, verticesFromWavefrontWithSameRoot);
                newWavefrontCreated |= AddWavefrontIfValid(relevantVertices, rootVertex, distanceToRootFromSource, 0,
                    toAngle, verticesFromWavefrontWithSameRoot);
            }
            else
            {
                newWavefrontCreated |= AddWavefrontIfValid(relevantVertices, rootVertex,
                    distanceToRootFromSource, fromAngle, toAngle, verticesFromWavefrontWithSameRoot);
            }

            return newWavefrontCreated;
        }

        /// <summary>
        /// Creates a new wavelet and only adds it to the heap of all wavelets if it's valid.
        /// </summary>
        /// <param name="relevantVertices">All vertices that should be considered. They will be filtered so that only
        /// relevant vertices are used in the wavelet.</param>
        /// <param name="rootVertex">The root vertex of the wavelet.</param>
        /// <param name="distanceFromSourceToVertex">The distance from the source to the given root vertex.</param>
        /// <param name="fromAngle">From angle of the new wavelet.</param>
        /// <param name="toAngle">To angle of the new wavelet</param>
        /// <param name="verticesFromWavefrontWithSameRoot">True when the given vertices come from a wavelet with the
        /// same root. This is a performance tweak to skip sorting the vertices.</param>
        /// <returns>True when wavelet was created and added, false otherwise.</returns>
        public bool AddWavefrontIfValid(ICollection<Vertex> relevantVertices,
            Vertex rootVertex, double distanceFromSourceToVertex,
            double fromAngle, double toAngle, bool verticesFromWavefrontWithSameRoot)
        {
            var newWavefront = Wavelet.New(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex, verticesFromWavefrontWithSameRoot);
            if (newWavefront != null)
            {
                AddWavefront(newWavefront);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the most relevant vertex from the vertex queue of the wavelet and also removes the whole wavelet
        /// from the list of wavelets.
        /// </summary>
        private void RemoveAndUpdateWavefront(FibonacciHeapNode<Wavelet, double> waveletNode)
        {
            var wavelet = waveletNode.Data;
            Wavefronts.RemoveMin();
            wavelet.RemoveNextVertex();
        }

        /// <summary>
        /// Adds the given wavelet to the current heap if the distance to the next vertex is not zero.
        /// </summary>
        public void AddWavefront(Wavelet wavelet)
        {
            if (wavelet.DistanceToNextVertex == 0)
            {
                return;
            }

            Wavefronts.Insert(
                new FibonacciHeapNode<Wavelet, double>(wavelet, wavelet.DistanceToNextVertex));
        }
    }
}