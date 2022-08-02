using Mars.Common;
using Mars.Common.Collections;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront
{
    public class WavefrontAlgorithm
    {
        private readonly int knnSearchNeighbors = 100;
        
        private readonly List<Obstacle> _obstacles;
        private readonly Dictionary<Vertex, List<Vertex>> _vertexNeighbors;

        public readonly Dictionary<Position, Position?> PositionToPredecessor;
        public readonly FibonacciHeap<Wavefront, double> Wavefronts;
        public readonly List<Vertex> Vertices;

        public WavefrontAlgorithm(List<Obstacle> obstacles)
        {
            _obstacles = obstacles;
            PositionToPredecessor = new Dictionary<Position, Position?>();
            Wavefronts = new FibonacciHeap<Wavefront, double>(0);

            var positionToNeighbors = GetNeighborsFromObstacleVertices(_obstacles);
            Vertices = positionToNeighbors.Keys.Map(position => new Vertex(position, positionToNeighbors[position]));
            _vertexNeighbors = WavefrontPreprocessor.CalculateKnn(_obstacles, Vertices, knnSearchNeighbors);
        }

        public Dictionary<Position, List<Position>> GetNeighborsFromObstacleVertices(
            List<Obstacle> obstacles)
        {
            var positionToNeighbors = new Dictionary<Position, List<Position>>();
            obstacles.Each(obstacle =>
            {
                if (obstacle.Coordinates.Count <= 1)
                {
                    return;
                }

                var coordinates = obstacle.Coordinates.CreateCopy();
                if (obstacle.IsClosed)
                {
                    coordinates.RemoveAt(coordinates.Count - 1);
                }

                coordinates.Each((index, coordinate) =>
                {
                    var position = coordinate.ToPosition();
                    if (!positionToNeighbors.ContainsKey(position))
                    {
                        positionToNeighbors[position] = new List<Position>();
                    }

                    Coordinate? nextCoordinate =
                        index + 1 < coordinates.Count ? coordinates[index + 1] : null;
                    Coordinate? previousCoordinate = index - 1 >= 0 ? coordinates[index - 1] : null;
                    if (obstacle.IsClosed && nextCoordinate == null)
                    {
                        nextCoordinate = coordinates.First();
                    }

                    if (obstacle.IsClosed && previousCoordinate == null)
                    {
                        previousCoordinate = coordinates[^1];
                    }

                    if (nextCoordinate != null)
                    {
                        positionToNeighbors[position].Add(nextCoordinate.ToPosition());
                    }

                    if (previousCoordinate != null)
                    {
                        positionToNeighbors[position].Add(previousCoordinate.ToPosition());
                    }
                });
            });
            return positionToNeighbors;
        }

        public List<Position> Route(Position source, Position target)
        {
            var targetVertex = new Vertex(target);
            Vertices.Add(targetVertex);
            var neighborsOfTarget = WavefrontPreprocessor.GetNeighborsForVertex(_obstacles, Vertices, targetVertex, knnSearchNeighbors);
            
            // TODO Find a better way to add the target to the existing neighbors lists?
            neighborsOfTarget.Each(neighbor => _vertexNeighbors[neighbor].Add(targetVertex));


            PositionToPredecessor[source] = null;

            var sourceVertex = new Vertex(source);
            _vertexNeighbors[sourceVertex] =
                WavefrontPreprocessor.GetNeighborsForVertex(_obstacles, Vertices, sourceVertex, knnSearchNeighbors);
            var initialWavefront = Wavefront.New(0, 360, sourceVertex, _vertexNeighbors[sourceVertex], 0, false);
            if (initialWavefront == null)
            {
                return new List<Position>();
            }

            AddWavefront(initialWavefront);

            Log.Init();
            Log.I($"Routing from {source} to {target}");
            Log.D($"Initial wavefront at {initialWavefront.RootVertex.Position}");

            while (!PositionToPredecessor.ContainsKey(target))
            {
                ProcessNextEvent(target);
            }
            
            // Clean up the list for future uses of the Route() method
            neighborsOfTarget.Each(neighbor => _vertexNeighbors[neighbor].Remove(targetVertex));

            var waypoints = new List<Position>();
            var nextPosition = target;

            while (nextPosition != null)
            {
                waypoints.Add(nextPosition);
                nextPosition = PositionToPredecessor.ContainsKey(nextPosition)
                    ? PositionToPredecessor[nextPosition]
                    : null;
            }

            waypoints.Reverse();
            return waypoints;
        }

        public void ProcessNextEvent(Position targetPosition)
        {
            var wavefrontNode = Wavefronts.Min();
            var wavefront = wavefrontNode.Data;
            var currentVertex = wavefront.GetNextVertex();

            if (currentVertex == null)
            {
                Log.D("No next vertex, remove wavefront");
                // This wavefront doesn't have any events ahead, to we can remove it. 
                Wavefronts.RemoveMin();
                return;
            }

            var currentVertexHasBeenVisitedBefore = PositionToPredecessor.ContainsKey(currentVertex.Position);
            if (currentVertexHasBeenVisitedBefore)
            {
                Log.D($"Vertex at {currentVertex.Position} has been visited before");
                RemoveAndUpdateWavefront(wavefrontNode);
                AddWavefront(wavefront);
                return;
            }

            // var isCurrentVertexVisible = IsPositionVisible(wavefront.RootVertex.Position, currentVertex.Position);
            // if (!isCurrentVertexVisible)
            // {
            //     Log.D($"Vertex at {currentVertex.Position} is not visible");
            //     RemoveAndUpdateWavefront(wavefrontNode);
            //     AddWavefront(wavefront);
            //     return;
            // }

            if (Equals(currentVertex.Position, targetPosition))
            {
                Log.I($"Target reached ({currentVertex.Position})", "", 1);
                PositionToPredecessor[currentVertex.Position] = wavefront.RootVertex.Position;
                Log.D($"Set predecessor of target to {wavefront.RootVertex.Position}");
                RemoveAndUpdateWavefront(wavefrontNode);
                return;
            }

            Log.D($"Next vertex at {currentVertex.Position}");

            RemoveAndUpdateWavefront(wavefrontNode);
            Log.D("Drop vertex from wavefront");

            double angleShadowFrom;
            double angleShadowTo;

            HandleNeighbors(currentVertex, wavefront, out angleShadowFrom, out angleShadowTo,
                out var newWavefrontCreatedAtEventRoot);
            Log.D($"Handled neighbors, shadow from {angleShadowFrom}° to {angleShadowTo}°");

            if (newWavefrontCreatedAtEventRoot)
            {
                PositionToPredecessor[currentVertex.Position] = wavefront.RootVertex.Position;
                Log.D($"Set predecessor of {currentVertex.Position} to {wavefront.RootVertex.Position}");
            }

            if (!Double.IsNaN(angleShadowFrom) && !Double.IsNaN(angleShadowTo))
            {
                Log.D($"Remove old wavefront from {wavefront.FromAngle}° to {wavefront.ToAngle}° and create new ones");

                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    wavefront.FromAngle, angleShadowFrom, true);
                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    angleShadowTo, wavefront.ToAngle, true);
            }
            else
            {
                AddWavefront(wavefront);
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
            Log.D($"Process vertex={currentVertex}");

            angleShadowFrom = Double.NaN;
            angleShadowTo = Double.NaN;
            createdWavefrontAtCurrentVertex = false;

            var rightNeighbor = currentVertex.RightNeighbor(wavefront.RootVertex.Position) ??
                                currentVertex.LeftNeighbor(wavefront.RootVertex.Position);
            var leftNeighbor = currentVertex.LeftNeighbor(wavefront.RootVertex.Position) ??
                               currentVertex.RightNeighbor(wavefront.RootVertex.Position);

            if (rightNeighbor == null && leftNeighbor == null)
            {
                Log.D("Current vertex has no neighbors -> abort");
                return;
            }

            Log.D($"rightNeighbor={rightNeighbor}, leftNeighbor={leftNeighbor}");

            var angleRootToRightNeighbor = rightNeighbor != null
                ? Angle.GetBearing(wavefront.RootVertex.Position, rightNeighbor)
                : double.NaN;
            var angleRootToLeftNeighbor = leftNeighbor != null
                ? Angle.GetBearing(wavefront.RootVertex.Position, leftNeighbor)
                : double.NaN;
            var angleRootToCurrentVertex = Angle.GetBearing(wavefront.RootVertex.Position, currentVertex.Position);

            var angleVertexToRightNeighbor = rightNeighbor != null
                ? Angle.GetBearing(currentVertex.Position, rightNeighbor)
                : double.NaN;
            var angleVertexToLeftNeighbor = leftNeighbor != null
                ? Angle.GetBearing(currentVertex.Position, leftNeighbor)
                : double.NaN;

            // Rotate such that the current vertex of always north/up of the wavefront root
            var rotationAngle = -angleRootToCurrentVertex;
            Log.D($"Rotate relevant angles by {rotationAngle}°");
            var angleCurrentWavefrontFrom = Angle.Normalize(wavefront.FromAngle + rotationAngle);
            var angleCurrentWavefrontTo = Angle.Normalize(wavefront.ToAngle + rotationAngle);
            angleVertexToRightNeighbor = Angle.Normalize(angleVertexToRightNeighbor + rotationAngle);
            angleVertexToLeftNeighbor = Angle.Normalize(angleVertexToLeftNeighbor + rotationAngle);
            Log.Note($"angleCurrentWavefrontFrom={angleCurrentWavefrontFrom}");
            Log.Note($"angleCurrentWavefrontTo={angleCurrentWavefrontTo}");
            Log.Note($"angleVertexToRightNeighbor={angleVertexToRightNeighbor}");
            Log.Note($"angleVertexToLeftNeighbor={angleVertexToLeftNeighbor}");

            var rightNeighborHasBeenVisited = wavefront.HasBeenVisited(rightNeighbor);
            var leftNeighborHasBeenVisited = wavefront.HasBeenVisited(leftNeighbor);
            Log.Note($"rightNeighborHasBeenVisited={rightNeighborHasBeenVisited}");
            Log.Note($"leftNeighborHasBeenVisited={leftNeighborHasBeenVisited}");

            // Both neighbors on left/right (aka west/east) side of root+current vertex -> New wavefront needed for the casted shadow
            var bothNeighborsOnWestSide = Angle.GreaterEqual(angleVertexToRightNeighbor, 180) &&
                                          Angle.GreaterEqual(angleVertexToLeftNeighbor, 180);
            var bothNeighborsOnEastSide = Angle.LowerEqual(angleVertexToRightNeighbor, 180) &&
                                          Angle.LowerEqual(angleVertexToLeftNeighbor, 180);
            var currentVertexIsNeighbor = Equals(wavefront.RootVertex.Position, rightNeighbor) ||
                                          Equals(wavefront.RootVertex.Position, leftNeighbor);

            Log.Note($"bothNeighborsOnWestSide={bothNeighborsOnWestSide}");
            Log.Note($"bothNeighborsOnEastSide={bothNeighborsOnEastSide}");
            Log.Note($"currentVertexIsNeighbor={currentVertexIsNeighbor}");

            double angleWavefrontFrom = Double.NaN;
            double angleWavefrontTo = Double.NaN;
            if (bothNeighborsOnWestSide && !Angle.AreEqual(angleCurrentWavefrontTo, 360))
            {
                Log.Note("Both neighbors on west side");
                angleWavefrontFrom = Math.Max(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                angleWavefrontTo = 360;
            }
            else if (bothNeighborsOnEastSide && !Angle.AreEqual(angleCurrentWavefrontFrom, 0))
            {
                Log.Note("Both neighbors on east side");
                angleWavefrontFrom = 0;
                angleWavefrontTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
            }

            Log.D($"Wavefront goes from={angleWavefrontFrom}° to={angleWavefrontTo}°");

            // Wavefront root vertex is the only neighbor aka we reached the end of a line with the wavefront rooted
            // in the second last vertex of that line.
            var wavefrontRootIsSecondLastLineVertex = Equals(rightNeighbor, wavefront.RootVertex.Position) &&
                                                      Equals(leftNeighbor, wavefront.RootVertex.Position);
            if (wavefrontRootIsSecondLastLineVertex)
            {
                Log.D("Wavefront is second last in line string -> Create new wavefront");
                if (Angle.AreEqual(angleCurrentWavefrontTo, 0))
                {
                    angleWavefrontFrom = 0;
                    angleWavefrontTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                }
                else if (Angle.AreEqual(angleCurrentWavefrontFrom, 0))
                {
                    angleWavefrontFrom = Math.Max(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                    angleWavefrontTo = 360;
                }

                Log.Note($"Wavefront goes from={angleWavefrontFrom}° to={angleWavefrontTo}°");
            }

            var neighborWillBeVisitedByWavefront = wavefrontRootIsSecondLastLineVertex &&
                                                   Angle.IsBetween(angleCurrentWavefrontFrom, angleWavefrontFrom,
                                                       angleCurrentWavefrontTo);
            var newWavefrontNeeded = !double.IsNaN(angleWavefrontFrom) && !double.IsNaN(angleWavefrontTo) &&
                                     !neighborWillBeVisitedByWavefront;

            Log.D(
                $"New wavefront needed={newWavefrontNeeded} because: neighborWillBeVisitedByWavefront={neighborWillBeVisitedByWavefront}, " +
                $"angleWavefrontFrom={angleWavefrontFrom}°, angleWavefrontTo={angleWavefrontTo}°");

            // Rotate back
            Log.D($"Rotate every angle back by {-rotationAngle}°");
            angleWavefrontFrom = Angle.Normalize(angleWavefrontFrom - rotationAngle);
            angleWavefrontTo = Angle.Normalize(angleWavefrontTo - rotationAngle);

            if (newWavefrontNeeded)
            {
                createdWavefrontAtCurrentVertex = AddNewWavefront(currentVertex,
                    wavefront.DistanceTo(currentVertex.Position), angleWavefrontFrom, angleWavefrontTo, false);
            }

            double angleRightShadowFrom = Double.NaN;
            double angleRightShadowTo = Double.NaN;
            if (rightNeighborHasBeenVisited)
            {
                Angle.GetEnclosingAngles(angleRootToRightNeighbor, angleRootToCurrentVertex, out angleRightShadowFrom,
                    out angleRightShadowTo);
                angleShadowFrom = angleRightShadowFrom;
                angleShadowTo = angleRightShadowTo;
                Log.D(
                    $"Right neighbor={rightNeighbor} has been visited casting a shadow from {angleShadowFrom}° to {angleShadowTo}°");
            }

            double angleLeftShadowFrom = Double.NaN;
            double angleLeftShadowTo = Double.NaN;
            if (leftNeighborHasBeenVisited)
            {
                Angle.GetEnclosingAngles(angleRootToLeftNeighbor, angleRootToCurrentVertex, out angleLeftShadowFrom,
                    out angleLeftShadowTo);
                angleShadowFrom = angleLeftShadowFrom;
                angleShadowTo = angleLeftShadowTo;
                Log.D(
                    $"Left neighbor={leftNeighbor} has been visited casting a shadow from {angleLeftShadowFrom}° to {angleLeftShadowTo}°");
            }

            // When two shadows exist -> merge them because they always touch
            if (!Double.IsNaN(angleRightShadowFrom) && !Double.IsNaN(angleLeftShadowFrom))
            {
                Log.D("There are two shadows -> merge them");
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

                Log.D($"There were two shadows -> merged shadow goes from {angleShadowFrom}° to {angleShadowTo}°");
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
            Log.D(
                $"Angles for new wavefront (from={fromAngle}°, to={toAngle}°) exceed 0° border? {Angle.IsBetween(fromAngle, 0, toAngle)}");
            if (Angle.IsBetween(fromAngle, 0, toAngle))
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
                Log.D(
                    $"New wavefront at {newWavefront.RootVertex.Position} with {newWavefront.RelevantVertices.Count} relevant vertices from {fromAngle}° to {toAngle}°");
                Wavefronts.Insert(
                    new FibonacciHeapNode<Wavefront, double>(newWavefront, newWavefront.DistanceToNextVertex));
                return true;
            }

            Log.D(
                $"New wavefront at {rootVertex.Position} from {fromAngle}° to {toAngle}° wouldn't have any vertices -> ignore it");
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