using Mars.Common;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront
{
    public class WavefrontAlgorithm
    {
        private readonly List<Obstacle> Obstacles;

        public readonly Dictionary<Position, Position?> PositionToPredecessor;
        public readonly LinkedList<Wavefront> Wavefronts;
        public readonly LinkedList<Vertex> Vertices;

        public WavefrontAlgorithm(List<NetTopologySuite.Geometries.Geometry> obstacles,
            LinkedList<Wavefront>? wavefronts = null)
        {
            Obstacles = obstacles.Map(geometry => new Obstacle(geometry));
            Vertices = new LinkedList<Vertex>();
            PositionToPredecessor = new Dictionary<Position, Position?>();

            var positionToNeighbors = GetNeighborsFromObstacleVertices(Obstacles);
            positionToNeighbors.Keys.Each(position =>
            {
                Vertices.AddFirst(new Vertex(position, positionToNeighbors[position]));
            });

            Wavefronts = wavefronts ?? new LinkedList<Wavefront>();
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
            Vertices.AddFirst(new Vertex(target));
            PositionToPredecessor[source] = null;

            var initialWavefront = Wavefront.New(0, 360, new Vertex(source), Vertices, 0);
            if (initialWavefront == null)
            {
                return new List<Position>();
            }

            AddWavefront(initialWavefront);

            Log.Init();
            Log.I($"Routing from {source} to {target}");
            Log.D($"Initial wavefront at {initialWavefront.RootVertex.Position}");
            // Log.D($"Wavefront vertices {initialWavefront.RelevantVertices.Map(v => v.Position.ToString()).Join(", ")}");

            while (!PositionToPredecessor.ContainsKey(target))
            {
                ProcessNextEvent(target);
            }

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
            var wavefrontNode = Wavefronts.First;
            var wavefront = wavefrontNode.Value;
            var currentVertex = wavefront.GetNextVertex();

            // Log.D(
            //     $"Process wavefront (" +
            //     $"total wavefront={Wavefronts.Count}," +
            //     $"m from start={(int)wavefront.DistanceToRootFromSource}," +
            //     $"m to target={(int)wavefront.RootVertex.Position.DistanceInMTo(targetPosition)})" +
            //     $"at {wavefront.RootVertex.Position} from {wavefront.FromAngle}° to {wavefront.ToAngle}°",
            //     "",
            //     1);

            if (currentVertex == null)
            {
                Log.D("No next vertex, remove wavefront");
                // This wavefront doesn't have any events ahead, to we can remove it. 
                Wavefronts.Remove(wavefrontNode);
                return;
            }

            var currentVertexHasBeenVisitedBefore = PositionToPredecessor.ContainsKey(currentVertex.Position);
            if (currentVertexHasBeenVisitedBefore)
            {
                Log.D($"Vertex at {currentVertex.Position} has been visited before");
                RemoveNextVertex(wavefrontNode);
                return;
            }

            var isCurrentVertexVisible = IsPositionVisible(wavefront.RootVertex.Position, currentVertex.Position);
            if (!isCurrentVertexVisible)
            {
                Log.D($"Vertex at {currentVertex.Position} is not visible");
                RemoveNextVertex(wavefrontNode);
                return;
            }

            if (Equals(currentVertex.Position, targetPosition))
            {
                Log.I($"Target reached ({currentVertex.Position})", "", 1);
                PositionToPredecessor[currentVertex.Position] = wavefront.RootVertex.Position;
                Log.D($"Set predecessor of target to {wavefront.RootVertex.Position}");
                wavefront.RemoveNextVertex();
                Wavefronts.Remove(wavefrontNode);
                return;
            }

            Log.D($"Next vertex at {currentVertex.Position}");

            var wavefrontRemoved = RemoveNextVertex(wavefrontNode);
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
                if (!wavefrontRemoved)
                {
                    Wavefronts.Remove(wavefrontNode);
                }

                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    wavefront.FromAngle, angleShadowFrom);
                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    angleShadowTo, wavefront.ToAngle);
            }
        }

        private bool RemoveNextVertex(LinkedListNode<Wavefront> wavefrontNode)
        {
            wavefrontNode.Value.RemoveNextVertex();
            return MoveWavefrontToCorrectPosition(wavefrontNode);
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

                // angleShadowFrom = Math.Min(angleRootToRightNeighbor, angleRootToLeftNeighbor);
                // angleShadowTo = 360;
            }
            else if (bothNeighborsOnEastSide && !Angle.AreEqual(angleCurrentWavefrontFrom, 0))
            {
                Log.Note("Both neighbors on east side");
                angleWavefrontFrom = 0;
                angleWavefrontTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);

                // angleShadowFrom = 0;
                // angleShadowTo = Math.Max(angleRootToRightNeighbor, angleRootToLeftNeighbor);
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
                createdWavefrontAtCurrentVertex = AddNewWavefront(Vertices, currentVertex,
                    wavefront.DistanceTo(currentVertex.Position), angleWavefrontFrom, angleWavefrontTo);
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

        public bool AddNewWavefront(ICollection<Vertex> vertices, Vertex root, double distanceToRootFromSource,
            double fromAngle, double toAngle)
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
                newWavefrontCreated |= AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, 360);
                newWavefrontCreated |= AddWavefrontIfValid(vertices, distanceToRootFromSource, root, 0, toAngle);
            }
            else
            {
                var verticesInAngleArea = vertices.Where(v =>
                {
                    var bearing = Angle.GetBearing(root.Position, v.Position);
                    return fromAngle <= bearing && bearing <= toAngle;
                }).ToList();
                newWavefrontCreated |= AddWavefrontIfValid(verticesInAngleArea, distanceToRootFromSource, root,
                    fromAngle, toAngle);
            }

            return newWavefrontCreated;
        }

        public bool AddWavefrontIfValid(ICollection<Vertex> relevantVertices, double distanceFromSourceToVertex,
            Vertex rootVertex, double fromAngle, double toAngle)
        {
            var newWavefront = Wavefront.New(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex);
            if (newWavefront != null)
            {
                Log.D(
                    $"New wavefront at {newWavefront.RootVertex.Position} with {newWavefront.RelevantVertices.Count} relevant vertices from {fromAngle}° to {toAngle}°");
                // Log.D($"Relevant vertices: {newWavefront.RelevantVertices.Map(v => v.Position.ToString()).Join(", ")}");
                AddWavefront(newWavefront);
                return true;
            }

            Log.D(
                $"New wavefront at {rootVertex.Position} from {fromAngle}° to {toAngle}° wouldn't have any vertices -> ignore it");
            return false;
        }

        private bool IsPositionVisible(Position startPosition, Position endPosition)
        {
            return !TrajectoryCollidesWithObstacle(startPosition, endPosition);
        }

        public bool TrajectoryCollidesWithObstacle(Position startPosition, Position endPosition)
        {
            var envelope = new Envelope(startPosition.ToCoordinate(), endPosition.ToCoordinate());

            var coordinateStart = startPosition.ToCoordinate();
            var coordinateEnd = endPosition.ToCoordinate();

            foreach (var obstacle in Obstacles)
            {
                if (!obstacle.CanIntersect(envelope))
                {
                    continue;
                }

                if (obstacle.IntersectsWithLine(coordinateStart, coordinateEnd))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Moves the given wavefront to the correct position. If the wavefront has no next vertex, it'll be removed
        /// from the list of wavefronts.
        /// </summary>
        /// <returns>true when removed from list, false when still in list.</returns>
        private bool MoveWavefrontToCorrectPosition(LinkedListNode<Wavefront> wavefrontNode)
        {
            var distanceToNextVertex = wavefrontNode.Value.DistanceToNextVertex;
            Wavefronts.Remove(wavefrontNode);

            // Wavefront has no next vertex -> kill the wavefront by removing it from the list
            if (wavefrontNode.Value.DistanceToNextVertex == 0)
            {
                return true;
            }

            // The next wavefront vertex is further away -> we can start looking from the next wavefront as this list
            // is sorted increasingly.
            var node = wavefrontNode.Next;
            while (node != null)
            {
                if (node.Value.DistanceToNextVertex > distanceToNextVertex)
                {
                    break;
                }

                node = node.Next;
            }

            if (node == null)
            {
                Wavefronts.AddLast(wavefrontNode);
            }
            else
            {
                Wavefronts.AddBefore(node, wavefrontNode);
            }

            return false;
        }

        public void AddWavefront(Wavefront newWavefront)
        {
            var distanceToNextVertex = newWavefront.DistanceToNextVertex;
            var node = Wavefronts.First;
            while (node != null)
            {
                if (node.Value.DistanceToNextVertex > distanceToNextVertex)
                {
                    Wavefronts.AddBefore(node, newWavefront);
                    return;
                }

                node = node.Next;
            }

            Wavefronts.AddLast(newWavefront);
        }
    }
}