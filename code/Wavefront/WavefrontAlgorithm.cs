using System.Collections.Immutable;
using System.Diagnostics;
using Mars.Common.Collections;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront
{
    public class WavefrontAlgorithm
    {
        private readonly int knnSearchNeighbors = 100;

        private readonly QuadTree<Obstacle> _obstacles;
        private readonly Dictionary<Vertex, List<Vertex>> _vertexNeighbors;

        // Stores the predecessor of each visited vertex position. Recursively following the predecessors up to the
        // source gives the shortest path from the source to the given position.
        public Dictionary<Waypoint, Waypoint?> WaypointToPredecessor;
        public Dictionary<Position, Waypoint> PositionToWaypoint;

        // Stores the known wavefront roots and their predecessor so that a) we can determine the shortest route from a
        // position to the source and b) make sure that wavefronts are only spawned at newly visited vertices. 
        public Dictionary<Waypoint, Waypoint?> WavefrontRootPredecessor;
        public Dictionary<Position, Waypoint> WavefrontRootToWaypoint;

        public FibonacciHeap<Wavefront, double> Wavefronts;
        public readonly List<Vertex> Vertices;

        public WavefrontAlgorithm(List<Obstacle> obstacles)
        {
            _obstacles = new QuadTree<Obstacle>();
            obstacles.Each(obstacle => _obstacles.Insert(obstacle.Envelope, obstacle));

            Log.I("Get direct neighbors on each obstacle geometry");
            var positionToNeighbors = WavefrontPreprocessor.GetNeighborsFromObstacleVertices(obstacles);

            Log.I("Create map of direct neighbor vertices on the obstacle geometries");
            Vertices = positionToNeighbors.Keys.Map(position => new Vertex(position, positionToNeighbors[position]));

            Log.I("Calculate KNN to get visible vertices");
            _vertexNeighbors = WavefrontPreprocessor.CalculateVisibleKnn(_obstacles, Vertices, knnSearchNeighbors);
        }

        private void Reset()
        {
            WaypointToPredecessor = new Dictionary<Waypoint, Waypoint?>();
            PositionToWaypoint = new Dictionary<Position, Waypoint>();
            WavefrontRootPredecessor = new Dictionary<Waypoint, Waypoint?>();
            WavefrontRootToWaypoint = new Dictionary<Position, Waypoint>();
            Wavefronts = new FibonacciHeap<Wavefront, double>(0);
        }

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

            var initialWavefront = Wavefront.New(0, 360, sourceVertex, _vertexNeighbors[sourceVertex], 0, false);
            if (initialWavefront == null)
            {
                return new RoutingResult();
            }

            AddWavefront(initialWavefront);

            Log.Init(0);
            Log.I($"Routing from {source} to {target}");
            Log.D($"Initial wavefront at {initialWavefront.RootVertex.Position}");

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

        public void ProcessNextEvent(Position targetPosition, Stopwatch stopwatch)
        {
            var wavefrontNode = Wavefronts.Min();
            var wavefront = wavefrontNode.Data;
            var currentVertex = wavefront.GetNextVertex();

            if (currentVertex == null)
            {
                // Log.D("No next vertex, remove wavefront");
                // This wavefront doesn't have any events ahead, to we can remove it. 
                Wavefronts.RemoveMin();
                return;
            }

            var currentVertexHasBeenVisitedBefore = WavefrontRootToWaypoint.ContainsKey(currentVertex.Position);
            if (currentVertexHasBeenVisitedBefore)
            {
                // Log.D($"Vertex at {currentVertex.Position} has been visited before");
                RemoveAndUpdateWavefront(wavefrontNode);
                AddWavefront(wavefront);
                return;
            }

            if (Equals(currentVertex.Position, targetPosition))
            {
                Log.I($"Target reached ({currentVertex.Position})", "", 1);
                SetPredecessor(currentVertex.Position, wavefront.RootVertex.Position, stopwatch, WaypointToPredecessor,
                    PositionToWaypoint);
                SetPredecessor(currentVertex.Position, wavefront.RootVertex.Position, stopwatch,
                    WavefrontRootPredecessor, WavefrontRootToWaypoint);
                // Log.D($"Set predecessor of target to {wavefront.RootVertex.Position}");
                RemoveAndUpdateWavefront(wavefrontNode);
                return;
            }

            // Log.D($"Next vertex at {currentVertex.Position}");

            RemoveAndUpdateWavefront(wavefrontNode);
            // Log.D("Drop vertex from wavefront");

            double angleShadowFrom;
            double angleShadowTo;

            HandleNeighbors(currentVertex, wavefront, out angleShadowFrom, out angleShadowTo,
                out var newWavefrontCreatedAtEventRoot);
            // Log.D($"Handled neighbors, shadow from {angleShadowFrom}° to {angleShadowTo}°");

            SetPredecessor(currentVertex.Position, wavefront.RootVertex.Position, stopwatch, WaypointToPredecessor,
                PositionToWaypoint);
            if (newWavefrontCreatedAtEventRoot)
            {
                SetPredecessor(currentVertex.Position, wavefront.RootVertex.Position, stopwatch,
                    WavefrontRootPredecessor, WavefrontRootToWaypoint);
                // Log.D($"Set predecessor of {currentVertex.Position} to {wavefront.RootVertex.Position}");
            }

            if (!Double.IsNaN(angleShadowFrom) && !Double.IsNaN(angleShadowTo))
            {
                var fromAngleInShadowArea = Angle.IsBetween(angleShadowFrom, wavefront.FromAngle, angleShadowTo);
                var toAngleInShadowArea = Angle.IsBetween(angleShadowFrom, wavefront.ToAngle, angleShadowTo);

                if (fromAngleInShadowArea && toAngleInShadowArea)
                {
                    // Both ends of our wavelet are within the shadow area -> Just use the inverted shadow area as
                    // wavelet angle area. It cannot happen that the wavelets area is completely within the shadow area
                    // because that would mean this wavelet visited vertices outside its angle area and that's not
                    // possible. Therefore it's safe to use the inverted shadow area here.
                    AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex,
                        wavefront.DistanceToRootFromSource,
                        angleShadowTo, angleShadowFrom, true);
                }
                else if (fromAngleInShadowArea)
                {
                    AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex,
                        wavefront.DistanceToRootFromSource,
                        angleShadowTo, wavefront.ToAngle, true);
                }
                else if (toAngleInShadowArea)
                {
                    AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex,
                        wavefront.DistanceToRootFromSource,
                        wavefront.FromAngle, angleShadowFrom, true);
                }
                else
                {
                    // Shadow area is completely within the wavelets angle area -> Split out wavelet into two so that
                    // the area of the shadow is not covered anymore.
                    AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex,
                        wavefront.DistanceToRootFromSource,
                        wavefront.FromAngle, angleShadowFrom, true);
                    AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex,
                        wavefront.DistanceToRootFromSource,
                        angleShadowTo, wavefront.ToAngle, true);
                }
            }
            else
            {
                AddWavefront(wavefront);
            }
        }

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

        private void RemoveAndUpdateWavefront(FibonacciHeapNode<Wavefront, double> wavefrontNode)
        {
            var wavefront = wavefrontNode.Data;
            Wavefronts.RemoveMin();
            wavefront.RemoveNextVertex();
        }

        // TODO document rotation idea of this method
        public void HandleNeighbors(Vertex currentVertex, Wavefront wavefront, out double angleShadowFrom,
            out double angleShadowTo, out bool createdWavefrontAtCurrentVertex)
        {
            // Log.D($"Process vertex={currentVertex}");

            angleShadowFrom = Double.NaN;
            angleShadowTo = Double.NaN;
            createdWavefrontAtCurrentVertex = false;
            
            var angleRootToCurrentVertex = Angle.GetBearing(wavefront.RootVertex.Position, currentVertex.Position);
            var waveletAngleStartsAtVertex = Angle.AreEqual(wavefront.FromAngle, angleRootToCurrentVertex);
            var waveletAngleEndsAtVertex = Angle.AreEqual(wavefront.ToAngle, angleRootToCurrentVertex);
            
            var rightNeighbor = currentVertex.RightNeighbor(wavefront.RootVertex.Position, waveletAngleStartsAtVertex) ??
                                currentVertex.LeftNeighbor(wavefront.RootVertex.Position, waveletAngleEndsAtVertex);
            var leftNeighbor = currentVertex.LeftNeighbor(wavefront.RootVertex.Position, waveletAngleEndsAtVertex) ??
                               currentVertex.RightNeighbor(wavefront.RootVertex.Position, waveletAngleStartsAtVertex);

            if (rightNeighbor == null && leftNeighbor == null)
            {
                // Log.D("Current vertex has no neighbors -> abort");
                return;
            }

            // Log.D($"rightNeighbor={rightNeighbor}, leftNeighbor={leftNeighbor}");

            var angleRootToRightNeighbor = rightNeighbor != null
                ? Angle.GetBearing(wavefront.RootVertex.Position, rightNeighbor)
                : double.NaN;
            var angleRootToLeftNeighbor = leftNeighbor != null
                ? Angle.GetBearing(wavefront.RootVertex.Position, leftNeighbor)
                : double.NaN;

            var angleVertexToRightNeighbor = rightNeighbor != null
                ? Angle.GetBearing(currentVertex.Position, rightNeighbor)
                : double.NaN;
            var angleVertexToLeftNeighbor = leftNeighbor != null
                ? Angle.GetBearing(currentVertex.Position, leftNeighbor)
                : double.NaN;

            // Rotate such that the current vertex of always north/up of the wavefront root
            var rotationAngle = -angleRootToCurrentVertex;
            // Log.D($"Rotate relevant angles by {rotationAngle}°");
            var angleCurrentWavefrontFrom = Angle.Normalize(wavefront.FromAngle + rotationAngle);
            var angleCurrentWavefrontTo = Angle.Normalize(wavefront.ToAngle + rotationAngle);
            angleVertexToRightNeighbor = Angle.Normalize(angleVertexToRightNeighbor + rotationAngle);
            angleVertexToLeftNeighbor = Angle.Normalize(angleVertexToLeftNeighbor + rotationAngle);
            // Log.Note($"angleCurrentWavefrontFrom={angleCurrentWavefrontFrom}");
            // Log.Note($"angleCurrentWavefrontTo={angleCurrentWavefrontTo}");
            // Log.Note($"angleVertexToRightNeighbor={angleVertexToRightNeighbor}");
            // Log.Note($"angleVertexToLeftNeighbor={angleVertexToLeftNeighbor}");

            var rightNeighborHasBeenVisited = wavefront.HasBeenVisited(rightNeighbor);
            var leftNeighborHasBeenVisited = wavefront.HasBeenVisited(leftNeighbor);
            // Log.Note($"rightNeighborHasBeenVisited={rightNeighborHasBeenVisited}");
            // Log.Note($"leftNeighborHasBeenVisited={leftNeighborHasBeenVisited}");

            // Both neighbors on left/right (aka west/east) side of root+current vertex -> New wavefront needed for the casted shadow
            var bothNeighborsOnWestSide = Angle.GreaterEqual(angleVertexToRightNeighbor, 180) &&
                                          Angle.GreaterEqual(angleVertexToLeftNeighbor, 180);
            var bothNeighborsOnEastSide = Angle.LowerEqual(angleVertexToRightNeighbor, 180) &&
                                          Angle.LowerEqual(angleVertexToLeftNeighbor, 180);

            // Log.Note($"bothNeighborsOnWestSide={bothNeighborsOnWestSide}");
            // Log.Note($"bothNeighborsOnEastSide={bothNeighborsOnEastSide}");
            // Log.Note($"currentVertexIsNeighbor={currentVertexIsNeighbor}");

            double angleNewWavefrontFrom = Double.NaN;
            double angleNewWavefrontTo = Double.NaN;
            if (bothNeighborsOnWestSide && !Angle.AreEqual(angleCurrentWavefrontTo, 360))
            {
                Log.Note("Both neighbors on west side");
                angleNewWavefrontFrom = Math.Max(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                angleNewWavefrontTo = 360;
            }
            else if (bothNeighborsOnEastSide && !Angle.AreEqual(angleCurrentWavefrontFrom, 0))
            {
                Log.Note("Both neighbors on east side");
                angleNewWavefrontFrom = 0;
                angleNewWavefrontTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
            }

            // Log.D($"Wavefront goes from={angleWavefrontFrom}° to={angleWavefrontTo}°");

            // Wavefront root vertex is the only neighbor. In other words we reached the end of a line and the wavefront
            // root vertex is the second last vertex of that line.
            var wavefrontRootIsSecondLastLineVertex = Equals(rightNeighbor, wavefront.RootVertex.Position) &&
                                                      Equals(leftNeighbor, wavefront.RootVertex.Position);

            // When wavelet is rooted at second last vertex of a line -> This end vertex of the line will be visited
            // by this wavefront anyway.
            // When the new wavelet starts within the range of the current one, then the start of the new wavelet will
            // be visited by the current wavelet as well.
            var neighborWillBeVisitedByWavefront = wavefrontRootIsSecondLastLineVertex &&
                                                   Angle.IsBetweenWithNormalize(angleCurrentWavefrontFrom,
                                                       angleNewWavefrontFrom,
                                                       angleCurrentWavefrontTo);
            var newWavefrontNeeded = !double.IsNaN(angleNewWavefrontFrom) && !double.IsNaN(angleNewWavefrontTo) &&
                                     !neighborWillBeVisitedByWavefront;

            // Log.D(
            // $"New wavefront needed={newWavefrontNeeded} because: neighborWillBeVisitedByWavefront={neighborWillBeVisitedByWavefront}, " +
            // $"angleWavefrontFrom={angleWavefrontFrom}°, angleWavefrontTo={angleWavefrontTo}°");

            // Rotate back
            // Log.D($"Rotate every angle back by {-rotationAngle}°");
            angleNewWavefrontFrom = Angle.Normalize(angleNewWavefrontFrom - rotationAngle);
            angleNewWavefrontTo = Angle.Normalize(angleNewWavefrontTo - rotationAngle);

            if (newWavefrontNeeded)
            {
                createdWavefrontAtCurrentVertex = AddNewWavefront(currentVertex,
                    wavefront.DistanceTo(currentVertex.Position), angleNewWavefrontFrom, angleNewWavefrontTo, false);
            }

            double angleRightShadowFrom = Double.NaN;
            double angleRightShadowTo = Double.NaN;
            if (rightNeighborHasBeenVisited)
            {
                Angle.GetEnclosingAngles(angleRootToRightNeighbor, angleRootToCurrentVertex, out angleRightShadowFrom,
                    out angleRightShadowTo);
                angleShadowFrom = angleRightShadowFrom;
                angleShadowTo = angleRightShadowTo;
                // Log.D(
                // $"Right neighbor={rightNeighbor} has been visited casting a shadow from {angleShadowFrom}° to {angleShadowTo}°");
            }

            double angleLeftShadowFrom = Double.NaN;
            double angleLeftShadowTo = Double.NaN;
            if (leftNeighborHasBeenVisited)
            {
                Angle.GetEnclosingAngles(angleRootToLeftNeighbor, angleRootToCurrentVertex, out angleLeftShadowFrom,
                    out angleLeftShadowTo);
                angleShadowFrom = angleLeftShadowFrom;
                angleShadowTo = angleLeftShadowTo;
                // Log.D(
                // $"Left neighbor={leftNeighbor} has been visited casting a shadow from {angleLeftShadowFrom}° to {angleLeftShadowTo}°");
            }

            // When two shadows exist -> merge them because they always touch
            if (!Double.IsNaN(angleRightShadowFrom) && !Double.IsNaN(angleLeftShadowFrom))
            {
                // Log.D("There are two shadows -> merge them");
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

                // Log.D($"There were two shadows -> merged shadow goes from {angleShadowFrom}° to {angleShadowTo}°");
            }
        }

        private bool AddNewWavefront(Vertex root, double distanceToRootFromSource, double fromAngle, double toAngle,
            bool verticesFromWavefrontWithSameRoot)
        {
            return AddNewWavefront(_vertexNeighbors[root], root, distanceToRootFromSource, fromAngle, toAngle,
                verticesFromWavefrontWithSameRoot);
        }

        public bool AddNewWavefront(ICollection<Vertex> vertices, Vertex root, double distanceToRootFromSource,
            double fromAngle, double toAngle, bool verticesFromWavefrontWithSameRoot)
        {
            bool newWavefrontCreated = false;

            toAngle = Angle.Normalize(toAngle);
            fromAngle = Angle.StrictNormalize(fromAngle);

            /*
             * If the interesting area exceeds the 0° border (e.g. goes from 300° via 0° to 40°), then we remove the
             * old wavefront and create two new ones. One from 300° to 360° and one from 0° to 40°. This simply
             * makes range checks easier and has no further reason.
             */
            // Log.D(
            // $"Angles for new wavefront (from={fromAngle}°, to={toAngle}°) exceed 0° border? {Angle.IsBetweenWithNormalize(fromAngle, 0, toAngle)}");
            if (Angle.IsBetweenWithNormalize(fromAngle, 0, toAngle))
            {
                newWavefrontCreated |= AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, 360,
                    verticesFromWavefrontWithSameRoot);
                newWavefrontCreated |= AddWavefrontIfValid(vertices, distanceToRootFromSource, root, 0, toAngle,
                    verticesFromWavefrontWithSameRoot);
            }
            else
            {
                newWavefrontCreated |= AddWavefrontIfValid(vertices, distanceToRootFromSource, root,
                    fromAngle, toAngle, verticesFromWavefrontWithSameRoot);
            }

            return newWavefrontCreated;
        }

        public bool AddWavefrontIfValid(ICollection<Vertex> relevantVertices, double distanceFromSourceToVertex,
            Vertex rootVertex, double fromAngle, double toAngle, bool verticesFromWavefrontWithSameRoot)
        {
            var newWavefront = Wavefront.New(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex, verticesFromWavefrontWithSameRoot);
            if (newWavefront != null)
            {
                // Log.D(
                // $"New wavefront at {newWavefront.RootVertex.Position} with {newWavefront.RelevantVertices.Count} relevant vertices from {fromAngle}° to {toAngle}°");
                Wavefronts.Insert(
                    new FibonacciHeapNode<Wavefront, double>(newWavefront, newWavefront.DistanceToNextVertex));
                return true;
            }

            // Log.D(
            // $"New wavefront at {rootVertex.Position} from {fromAngle}° to {toAngle}° wouldn't have any vertices -> ignore it");
            return false;
        }

        public void AddWavefront(Wavefront newWavefront)
        {
            if (newWavefront.DistanceToNextVertex == 0)
            {
                return;
            }

            Wavefronts.Insert(
                new FibonacciHeapNode<Wavefront, double>(newWavefront, newWavefront.DistanceToNextVertex));
        }
    }
}