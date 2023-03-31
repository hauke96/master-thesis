using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Collections.Graph.Algorithms;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Layers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront
{
    public class HybridGeometricRouter
    {
        private QuadTree<Obstacle> _obstacles;

        // Map from vertex to bins of neighboring vertices. The term "neighbor" here refers to all vertices with an edge
        // to the key vertex of a dict entry. The bins contain all visible neighbors within the angle area of the
        // obstacle neighbors of the key vertex (Vertex.Neighbors). The first bin contains the visible neighbors between
        // the first and second obstacle neighbors of the vertex, and so on.
        [Obsolete] private Dictionary<Vertex, List<List<Vertex>>> _vertexNeighbors;

        // Stores the predecessor of each visited vertex position. Recursively following the predecessors from a vertex
        // v to the source gives the shortest path from the source to v.
        [Obsolete] public Dictionary<Waypoint, Waypoint?> WaypointToPredecessor;
        [Obsolete] public Dictionary<Position, Waypoint> PositionToWaypoint;

        // Stores the known wavelet roots and their predecessor to make sure that wavelets are only spawned at vertices
        // where no other wavelet has been spawned yet. Following the predecessors from the target back to the source
        // yields the actual route the wavelets took and therefore the shortest path.
        [Obsolete] public Dictionary<Waypoint, Waypoint?> WaveletRootPredecessor;
        [Obsolete] public Dictionary<Position, Waypoint> WaveletRootToWaypoint;

        [Obsolete] public FibonacciHeap<Wavelet, double> Wavelets;
        [Obsolete] public readonly List<Vertex> Vertices;

        private readonly bool _debugModeActive;
        private readonly int _knnSearchNeighborBins;
        private readonly int _knnSearchNeighborsPerBin;

        private readonly SpatialGraph _graph;

        [Obsolete]
        public HybridGeometricRouter(IEnumerable<IFeature> obstacles, bool debugModeActive = false,
            int knnSearchNeighborBins = 36, int knnSearchNeighborsPerBin = 10)
        {
            _debugModeActive = debugModeActive;
            _knnSearchNeighborBins = knnSearchNeighborBins;
            _knnSearchNeighborsPerBin = knnSearchNeighborsPerBin;
            _obstacles = WavefrontPreprocessor.SplitObstacles(obstacles, _debugModeActive);
            _vertexNeighbors =
                WavefrontPreprocessor.CalculateVisibleKnn(_obstacles, _knnSearchNeighborBins, _knnSearchNeighborsPerBin,
                    _debugModeActive);
            Vertices = _vertexNeighbors.Keys.ToList();

            Reset();
        }

        public HybridGeometricRouter(ICollection<IVectorFeature> features)
        {
            _graph = GraphGenerator.Generate(features);
        }

        /// <summary>
        /// Clears the results of a previous routing run.
        /// </summary>
        private void Reset()
        {
            WaypointToPredecessor = new Dictionary<Waypoint, Waypoint?>();
            PositionToWaypoint = new Dictionary<Position, Waypoint>();
            WaveletRootPredecessor = new Dictionary<Waypoint, Waypoint?>();
            WaveletRootToWaypoint = new Dictionary<Position, Waypoint>();
            Wavelets = new FibonacciHeap<Wavelet, double>(0);
        }

        public IList<EdgeData> Route(Position source, Position target)
        {
            // TODO Add source and target to graph, determine visibility neighbors, create edges and clean up graph afterwards
            
            var sourceNode = 0;
            var targetNode = 0;

            return _graph.AStarAlgorithm(sourceNode, targetNode,
                (edge, _) => edge.Length * (edge.Data.IsEmpty() ? 1 : 0.1));
        }

        /// <summary>
        /// Calculates the optimal geometric route from the source vertex to the target vertex.
        /// </summary>
        /// <returns>The optimal route as well as all other routes found during this method call. Finding these
        /// alternative routes stopped at the moment the target was reached.</returns>
        [Obsolete]
        public RoutingResult RouteLegacy(Position source, Position target)
        {
            var stopwatch = new Stopwatch();

            Reset();

            // The data will be changed (e.g. vertices and neighbor-relations added), so a copy is used for each routing call. 
            var vertices = Vertices.Map(v => v.clone());
            var vertexNeighbors = _vertexNeighbors.CreateCopy();

            var sourceVertex = new Vertex(source);
            vertices.Add(sourceVertex);

            var targetVertex = new Vertex(target);
            vertices.Add(targetVertex);

            SetPredecessor(source, null, stopwatch, WaypointToPredecessor, PositionToWaypoint, 0);
            SetPredecessor(source, null, stopwatch, WaveletRootPredecessor, WaveletRootToWaypoint, 0);

            vertexNeighbors[sourceVertex] =
                WavefrontPreprocessor.GetVisibilityNeighborsForVertex(_obstacles, vertices, sourceVertex,
                    _knnSearchNeighborBins);

            var neighborsOfTarget =
                WavefrontPreprocessor.GetVisibilityNeighborsForVertex(_obstacles, vertices, targetVertex,
                    _knnSearchNeighborBins);

            neighborsOfTarget.SelectMany(x => x).Each(neighbor =>
            {
                // Get all neighbor bins of "neighbor" in which the target vertex falls. This is usually just one bin
                // But might be two.
                var bearing = Angle.GetBearing(neighbor.Position, targetVertex.Position);
                vertexNeighbors[neighbor].Each((i, bin) =>
                {
                    var targetIsInBin = neighbor.ObstacleNeighbors.Count < 2 || Angle.IsBetweenEqual(
                        neighbor.ObstacleNeighbors[i].Bearing,
                        bearing,
                        neighbor.ObstacleNeighbors[(i + 1) % neighbor.ObstacleNeighbors.Count].Bearing
                    );

                    if (targetIsInBin)
                    {
                        bin.Add(targetVertex);
                    }
                });
            });

            // TODO Optimize this: When the target is visible from the source, we don't need any routing at all.
            if (!vertexNeighbors[sourceVertex].SelectMany(x => x).Contains(targetVertex) &&
                IsTargetVisibleFromSource(sourceVertex, targetVertex))
            {
                vertexNeighbors[sourceVertex][0].Add(targetVertex);
            }

            var allSourceVertexNeighbors = vertexNeighbors[sourceVertex].SelectMany(x => x).ToList();
            var initialWavelet = Wavelet.New(0, 360, sourceVertex, allSourceVertexNeighbors, 0, false);
            if (initialWavelet == null)
            {
                return new RoutingResult();
            }

            AddWavelet(initialWavelet);

            Log.I($"Routing from {source} to {target}");
            Log.D($"Initial wavelet at {initialWavelet.RootVertex.Position}");

            stopwatch.Start();
            while (!PositionToWaypoint.ContainsKey(target) && !Wavelets.IsEmpty())
            {
                ProcessNextEvent(target, stopwatch);
            }

            List<Waypoint> waypoints = new List<Waypoint>();

            var targetWaypoints = WaveletRootPredecessor.Keys.Where(k => k.Position.Equals(target)).ToList();
            if (!targetWaypoints.IsEmpty())
            {
                waypoints.AddRange(GetOptimalRoute(targetWaypoints.First(), WaveletRootPredecessor));
            }

            return new RoutingResult(waypoints, GetAllRoutes());
        }

        private bool IsTargetVisibleFromSource(Vertex source, Vertex target)
        {
            var envelope = new Envelope(source.Coordinate, target.Coordinate);
            var intersectsWithObstacle = false;
            var coordinateToObstacles = new Dictionary<Coordinate, List<Obstacle>>();
            _obstacles.Query(envelope, (Action<Obstacle>)(obstacle =>
            {
                if (intersectsWithObstacle || !obstacle.CanIntersect(envelope))
                {
                    return;
                }

                intersectsWithObstacle |=
                    obstacle.IntersectsWithLine(source.Coordinate, target.Coordinate, coordinateToObstacles);
            }));

            return !intersectsWithObstacle;
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
            var waveletNode = Wavelets.Min();
            var wavelet = waveletNode.Data;
            var currentVertex = wavelet.GetNextVertex();
            var distanceToCurrentVertexFromSource = wavelet.DistanceToNextVertex;

            if (currentVertex == null)
            {
                // This wavelet doesn't have any events ahead, to we can remove it. 
                Wavelets.RemoveMin();
                return;
            }

            var currentVertexWasRootOfWaveletBefore = WaveletRootToWaypoint.ContainsKey(currentVertex.Position);
            if (currentVertexWasRootOfWaveletBefore)
            {
                // The current vertex was already used as a root vertex of a wavelet. This means there are shorter paths
                // to this vertex and we can ignore it since the path using the current wavelet is not optimal.
                RemoveAndUpdateWavelet(waveletNode);
                AddWavelet(wavelet);
                return;
            }

            if (Equals(currentVertex.Position, targetPosition))
            {
                Log.I($"Target reached ({currentVertex.Position})");
                SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch, WaypointToPredecessor,
                    PositionToWaypoint, distanceToCurrentVertexFromSource);
                SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch,
                    WaveletRootPredecessor, WaveletRootToWaypoint, distanceToCurrentVertexFromSource);
                RemoveAndUpdateWavelet(waveletNode);
                return;
            }

            // Updating the current wavelet means that the current vertex is marked as "done" by removing it from the
            // wavelets vertex queue. This can be done since that vertex is processed right now.
            // The wavelet is then removed from the list because of the following strategy: If the current wavelet
            // should be split or adjusted, it's easier to just create new wavelet(s) and add them. If nothing needs to
            // be done to the current wavelet, it'll just be re-added to the list.
            RemoveAndUpdateWavelet(waveletNode);

            // The wavelet hit the current vertex which is (probably) connected to other vertices and these
            // lines/polygons might cast shadows. These shadow areas are areas that the current wavelet doesn't need to
            // consider anymore. However, that's handled below, this code here just finds the shadow areas and spawns
            // new wavelets if needed (e.g. wavelets behind a corner).
            double angleShadowFrom;
            double angleShadowTo;
            HandleNeighbors(currentVertex, wavelet, out angleShadowFrom, out angleShadowTo,
                out var newWaveletCreatedAtEventRoot);

            if (!newWaveletCreatedAtEventRoot.IsEmpty())
            {
                // A new wavelet has been spawned with its root equal to the currently visited vertex. This means that
                // the current vertex is now the root of a wavelet for the first time. This is the case because
                // otherwise the current vertex would not have been considered in the first place (s. exit conditions
                // above).
                SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch,
                    WaveletRootPredecessor, WaveletRootToWaypoint, distanceToCurrentVertexFromSource);
            }

            // Save the normal vertex predecessor relation for the current vertex since it's the first visit of this
            // vertex (s. exit conditions above). 
            SetPredecessor(currentVertex.Position, wavelet.RootVertex.Position, stopwatch, WaypointToPredecessor,
                PositionToWaypoint, distanceToCurrentVertexFromSource);

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
                    AddNewWavelet(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        angleShadowTo, angleShadowFrom, true);
                }
                else if (fromAngleInShadowArea)
                {
                    // Only the from-angle of the current wavelet is within the shadow area, so we just create one new
                    // wavelet with adjusted from-angle.
                    AddNewWavelet(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        angleShadowTo, wavelet.ToAngle, true);
                }
                else if (toAngleInShadowArea)
                {
                    // Only the to-angle of the current wavelet is within the shadow area, so we just create one new
                    // wavelet with adjusted to-angle.
                    AddNewWavelet(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        wavelet.FromAngle, angleShadowFrom, true);
                }
                else
                {
                    // Shadow area is completely within the wavelets angle area -> Split out wavelet into two so that
                    // the area of the shadow is not covered anymore.
                    AddNewWavelet(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        wavelet.FromAngle, angleShadowFrom, true);
                    AddNewWavelet(wavelet.RelevantVertices, wavelet.RootVertex,
                        wavelet.DistanceToRootFromSource,
                        angleShadowTo, wavelet.ToAngle, true);
                }
            }
            else
            {
                // Wavelet is not casting a shadow -> Re-add it because it's still valid.
                AddWavelet(wavelet);
            }
        }

        /// <summary>
        /// Stores the given predecessor position for the given vertex. This also stores metadata like the time this
        /// vertex has been visited and how many vertices had been visited before this one. 
        /// </summary>
        private void SetPredecessor(Position vertexPosition, Position? predecessorPosition, Stopwatch stopwatch,
            Dictionary<Waypoint, Waypoint?> waypointToPredecessor, Dictionary<Position, Waypoint> positionToWaypoint,
            double distanceFromSource)
        {
            Waypoint? predecessor = null;
            if (predecessorPosition != null)
            {
                positionToWaypoint.TryGetValue(predecessorPosition, out predecessor);
            }

            Waypoint waypoint;
            if (!positionToWaypoint.ContainsKey(vertexPosition))
            {
                waypoint = new Waypoint(vertexPosition, positionToWaypoint.Count, stopwatch.Elapsed.TotalMilliseconds,
                    distanceFromSource);
                waypointToPredecessor[waypoint] = predecessor;
                positionToWaypoint[vertexPosition] = waypoint;
            }
        }

        /// <summary>
        /// This method considers the given "currentVertex" to be visited by the given wavelet. The main part of this
        /// method is to determine whether or not a new wavelet is needed. Therefore, this method might create one or
        /// two (in case the 0° border is exceeded) new wavelets.
        ///
        /// In addition to that, based on the angles of "currentVertex"es neighbors, a shadow area cast from the given
        /// wavelet is determined.
        /// </summary>
        /// <param name="currentVertex">The vertex visited by the given wavelet.</param>
        /// <param name="wavelet">The wavelet visiting the given vertex.</param>
        /// <param name="angleShadowFrom">The resulting from-angle of a potential shadow area, NaN if no shadow is cast.</param>
        /// <param name="angleShadowTo">The resulting to-angle of a potential shadow area, NaN if no shadow is cast.</param>
        /// <param name="createdWaveletAtCurrentVertex">A list of created wavelets.</param>
        public void HandleNeighbors(Vertex currentVertex, Wavelet wavelet, out double angleShadowFrom,
            out double angleShadowTo, out List<Wavelet> createdWaveletAtCurrentVertex)
        {
            angleShadowFrom = Double.NaN;
            angleShadowTo = Double.NaN;
            createdWaveletAtCurrentVertex = new List<Wavelet>();

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
             * Rotate everything by -r° such that the current vertex is always north/up/at 0° of the wavelet root.
             * 
             * The idea behind it:
             * When the angles to the right and left neighbor are rotated by -r°, we can easily check if there's a
             * potential shadow casted by the neighbors. When both neighbors are on the east side (0°-180°), we know
             * that no other relevant neighbor is on the west side (180°-360°) and vice versa.
             * Knowing this is very helpful for detecting the need and angle of a new wavelet. It also helps to
             * determine the shadow area.
             */
            var rotationAngle = -angleRootToCurrentVertex;
            var angleCurrentWaveletFrom = Angle.Normalize(wavelet.FromAngle + rotationAngle);
            var angleCurrentWaveletTo = Angle.Normalize(wavelet.ToAngle + rotationAngle);
            angleRootToRightNeighbor = Angle.Normalize(angleRootToRightNeighbor + rotationAngle);
            angleRootToLeftNeighbor = Angle.Normalize(angleRootToLeftNeighbor + rotationAngle);
            angleVertexToRightNeighbor = Angle.Normalize(angleVertexToRightNeighbor + rotationAngle);
            angleVertexToLeftNeighbor = Angle.Normalize(angleVertexToLeftNeighbor + rotationAngle);

            // Determine if both neighbors are on the west, east or on both sides. This is important to know to
            // determine if a new wavelet is needed and with what angles. More details can be found further below.
            var bothNeighborsOnWestSide = Angle.IsBetweenEqual(180, angleVertexToRightNeighbor, 0) &&
                                          Angle.IsBetweenEqual(180, angleVertexToLeftNeighbor, 0);
            var bothNeighborsOnEastSide = Angle.IsBetweenEqual(0, angleVertexToRightNeighbor, 180) &&
                                          Angle.IsBetweenEqual(0, angleVertexToLeftNeighbor, 180);

            // To detect inner corners the wavelet just ran into, we have to know if exactly one of the neighbors is the
            // wavelet root vertex.
            var rootVertexIsRightNeighbor = rightNeighbor.Equals(wavelet.RootVertex.Position);
            var rootVertexIsLeftNeighbor = leftNeighbor.Equals(wavelet.RootVertex.Position);
            var exactlyOneNeighborIsWaveletRoot = rootVertexIsRightNeighbor && !rootVertexIsLeftNeighbor ||
                                                  rootVertexIsLeftNeighbor && !rootVertexIsRightNeighbor;

            double angleNewWaveletFrom = Double.NaN;
            double angleNewWaveletTo = Double.NaN;

            /*
             * Here two main cases are distinguished:
             * 
             *   1. Both neighbors are on the west AND east side. This means that one is at 0° and one at 180° or that
             *      both are at 180°.
             *      If one neighbor is at 0° and one at 180°, then we're dealing with collinear neighbors. In this case,
             *      one neighbor must be the wavelets root vertex. This means, that a new wavelet is needed with an
             *      angle area of 0° width towards the other neighbor, which is not the root vertex.
             *      If the neighbors are both at 180°, then they are both equal to the wavelets root and therefore
             *      the wavelet reached the end of a line segment. In this case, the new wavelet will have an angle
             *      area of 360° width, because the propagation is not bounded by any edge between the neighbors.
             * 
             *   2. The neighbors are either on the west or east side. This means that a new wavelet might be needed but
             *      it can also mean that the currentVertex is an inner corner and therefore no new wavelet is needed.
             *      To determine, whether the currentVertex is an inner corner or not, depends on the angles of the
             *      wavelet.
             *      The "else if"-blocks below describe the mechanism used to determine inner corners in more detail.
             *
             * To make the idea of the east/west considerations more clear, here's an example:
             * 
             *      V         V = Vertex (one of the neighbors)
             *       \
             *        X       X = Current vertex
             *        |
             *        |
             *        R       R = Root of wavelet and also a neighbors
             * 
             * Let V (an arbitrary unvisited vertex) and R (root of wavelet) both be neighbors and both on the west
             * side of an imaginary vertical line through R and X. Let's say the wavelet from R reached vertex X and has
             * an angle-area of 0° to 90° and that V is at 270° relative to X. Because both neighbors are on the west
             * side, we know, that the segment (X, V) does not cast a shadow. We also know, that this means, that
             * there's a need for an wavelet rooted in X with an angle area of 270° to 360°.
             *
             * Let's consider a different scenario where the wavelet rooted in R goes from 270° to 360°. In this case,
             * both neighbors and the wavelets angle area are all on the west side. This means the vertex X is an inner
             * corner since the wavelets angle area includes the segment (X, V).
             * In a case where V has been visited, this would be registered as a shadow cast by the segment (X, V). But
             * more on shadow areas below.
             */
            if (bothNeighborsOnEastSide && bothNeighborsOnWestSide)
            {
                // Neighbors are at 0° an 180° or both at 0° or 180°.
                if (Angle.AreEqual(angleVertexToRightNeighbor, 180) && Angle.AreEqual(angleVertexToLeftNeighbor, 180))
                {
                    // Both are at 180° -> We reached the end of a line

                    if (Angle.AreEqual(angleCurrentWaveletFrom, 0) && Angle.AreEqual(angleCurrentWaveletTo, 0))
                    {
                        // Wavelet has a 0° large area from 180° to 180°. This can happen when wavelet travels along
                        // collinear vertices. In such case, the new wavelet is a complete circle since we reached the 
                        // end of a line. We use the rotation angles here, since they will be subtracted below.
                        angleNewWaveletFrom = rotationAngle;
                        angleNewWaveletTo = 360 + rotationAngle;
                    }
                    else if (Angle.AreEqual(angleCurrentWaveletFrom, 0))
                    {
                        // Only from-angle of wavelet is at 180° -> Wavelet is on the right side of the line segment
                        angleNewWaveletFrom = 180;
                        angleNewWaveletTo = 360;
                    }
                    else if (Angle.AreEqual(angleCurrentWaveletTo, 360))
                    {
                        // Only to-angle of wavelet is at 180° -> Wavelet is on the left side of the line segment
                        angleNewWaveletFrom = 0;
                        angleNewWaveletTo = 180;
                    }
                    else
                    {
                        throw new Exception(
                            $"Error handling invalid state for collinear vertices:\n  angleCurrentWaveletFrom:{angleCurrentWaveletFrom}\n  angleCurrentWaveletTo: {angleCurrentWaveletTo}");
                    }
                }
                else
                {
                    // Both neighbors are on west & east side but angles are different -> collinear neighbors.
                    // This has no sub-cases, because we know that the next neighbor, that should be visited, is at 0°.
                    angleNewWaveletFrom = 0;
                    angleNewWaveletTo = 0;
                }
            }
            else if (bothNeighborsOnWestSide &&
                     !(Angle.AreEqual(angleCurrentWaveletTo, 360) && exactlyOneNeighborIsWaveletRoot))
            {
                // When both neighbors are on the west side. A to-Angle of 360° and one of the neighbors being the root
                // vertex, would mean that we reached an inner corner. This is not the case here, so we haven't reached
                // an inner corner and can create a wavelet.
                angleNewWaveletFrom = Math.Max(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                angleNewWaveletTo = 360;
            }
            else if (bothNeighborsOnEastSide &&
                     !(Angle.AreEqual(angleCurrentWaveletFrom, 0) && exactlyOneNeighborIsWaveletRoot))
            {
                // When both neighbors are on the east side. A from-Angle of 0° and one of the neighbors being the root
                // vertex, would mean that we reached an inner corner. This is not the case here, so we haven't reached
                // an inner corner and can create a wavelet.
                angleNewWaveletFrom = 0;
                angleNewWaveletTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
            }

            // When do we need a new wavelet? We must've determined some potential from- and to-angles above, which
            // means that there's a reason for a new wavelet in the first place.
            var newWaveletNeeded = !double.IsNaN(angleNewWaveletFrom) && !double.IsNaN(angleNewWaveletTo);

            // Rotate angles back to the actual values because now they are used to determine the return values.
            angleNewWaveletFrom = Angle.Normalize(angleNewWaveletFrom - rotationAngle);
            angleNewWaveletTo = Angle.Normalize(angleNewWaveletTo - rotationAngle);
            angleRootToRightNeighbor = Angle.Normalize(angleRootToRightNeighbor - rotationAngle);
            angleRootToLeftNeighbor = Angle.Normalize(angleRootToLeftNeighbor - rotationAngle);

            if (newWaveletNeeded)
            {
                double distanceToRootFromSource = wavelet.DistanceTo(currentVertex.Position);
                var currentVertexNeighbors = _vertexNeighbors[currentVertex].SelectMany(x => x).ToList();
                createdWaveletAtCurrentVertex = AddNewWavelet(currentVertexNeighbors, currentVertex,
                    distanceToRootFromSource, angleNewWaveletFrom, angleNewWaveletTo, false);
            }

            /*
             * Shadow areas:
             * 
             * Now the shadow areas to the right and left neighbors will be calculated. Only visited neighbors are
             * considered here, because it can happen that geometries are within the shadow area but closer to the
             * wavelets root than the respective neighbor. In such a scenario a shadow between the wavelet root and an
             * UNvisited neighbor would exclude geometries from being visited at all.
             *
             * Example:
             * Let R be the wavelets root, V the next visited vertex an X its neighbor. The vertices Y and Z would be
             * within the shadow area cast by the segment (V, X). If a shadow area is only determines *after* visiting
             * X, it would imply that Y and Z have been visited and handled before. In such case, it's safe to cast a
             * shadow since Y and Z are not relevant vertices for the given wavelet anymore.
             * 
             *                  X
             *               .`
             *            .`
             *         .`Y--Z
             *      .`
             *    V ..
             *         `` ..
             *               R
             */

            // Only consider edges to visited vertices to cast a shadow.
            var rightNeighborHasBeenVisited = wavelet.HasBeenVisited(rightNeighbor);
            var leftNeighborHasBeenVisited = wavelet.HasBeenVisited(leftNeighbor);

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
        /// <param name="verticesFromWaveletWithSameRoot">True when the given vertices come from a wavelet with the
        /// same root. This is a performance tweak to skip sorting the vertices.</param>
        /// <returns>True when wavelet was created and added, false otherwise.</returns>
        public List<Wavelet> AddNewWavelet(ICollection<Vertex> relevantVertices, Vertex rootVertex,
            double distanceToRootFromSource,
            double fromAngle, double toAngle, bool verticesFromWaveletWithSameRoot)
        {
            var newWavelets = new List<Wavelet?>();

            toAngle = Angle.Normalize(toAngle);
            fromAngle = Angle.StrictNormalize(fromAngle);

            /*
             * If the interesting area exceeds the 0° border (e.g. goes from 300° via 0° to 40°), then we remove the
             * old wavelet and create two new ones. One from 300° to 360° and one from 0° to 40°. This simply
             * makes range checks easier and has no further reason.
             */
            if (Angle.IsBetweenWithNormalize(fromAngle, 0, toAngle))
            {
                newWavelets.Add(AddWaveletIfValid(relevantVertices, rootVertex, distanceToRootFromSource,
                    fromAngle, 360, verticesFromWaveletWithSameRoot));
                newWavelets.Add(AddWaveletIfValid(relevantVertices, rootVertex, distanceToRootFromSource, 0,
                    toAngle, verticesFromWaveletWithSameRoot));
            }
            else
            {
                newWavelets.Add(AddWaveletIfValid(relevantVertices, rootVertex,
                    distanceToRootFromSource, fromAngle, toAngle, verticesFromWaveletWithSameRoot));
            }

            return newWavelets.WhereNotNull().ToList()!;
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
        /// <param name="verticesFromWaveletWithSameRoot">True when the given vertices come from a wavelet with the
        /// same root. This is a performance tweak to skip sorting the vertices.</param>
        /// <returns>True when wavelet was created and added, false otherwise.</returns>
        public Wavelet? AddWaveletIfValid(ICollection<Vertex> relevantVertices,
            Vertex rootVertex, double distanceFromSourceToVertex,
            double fromAngle, double toAngle, bool verticesFromWaveletWithSameRoot)
        {
            var newWavelet = Wavelet.New(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex, verticesFromWaveletWithSameRoot);
            if (newWavelet != null)
            {
                AddWavelet(newWavelet);
                return newWavelet;
            }

            return null;
        }

        /// <summary>
        /// Removes the most relevant vertex from the vertex queue of the wavelet and also removes the whole wavelet
        /// from the list of wavelets.
        /// </summary>
        private void RemoveAndUpdateWavelet(FibonacciHeapNode<Wavelet, double> waveletNode)
        {
            var wavelet = waveletNode.Data;
            Wavelets.RemoveMin();
            wavelet.RemoveNextVertex();
        }

        /// <summary>
        /// Adds the given wavelet to the current heap if the distance to the next vertex is not zero.
        /// </summary>
        public void AddWavelet(Wavelet wavelet)
        {
            if (wavelet.DistanceToNextVertex == 0)
            {
                return;
            }

            Wavelets.Insert(
                new FibonacciHeapNode<Wavelet, double>(wavelet, wavelet.DistanceToNextVertex));
        }
    }
}