using Mars.Common;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront
{
    public class WavefrontAlgorithm
    {
        private readonly List<NetTopologySuite.Geometries.Geometry> Obstacles;

        public readonly Dictionary<Position, Position?> PositionToPredecessor;
        public readonly List<Wavefront> Wavefronts;
        public readonly List<Vertex> Vertices;

        public WavefrontAlgorithm(List<NetTopologySuite.Geometries.Geometry> obstacles,
            List<Wavefront>? wavefronts = null)
        {
            Obstacles = obstacles;
            Vertices = new List<Vertex>();
            PositionToPredecessor = new Dictionary<Position, Position?>();

            Obstacles.Each(obstacle =>
            {
                var positions = obstacle.Coordinates.Map(c => new Vertex(c, obstacle));
                Vertices.AddRange(positions);
            });
            Wavefronts = wavefronts ?? new List<Wavefront>();
        }

        public List<Position> Route(Position source, Position target)
        {
            Vertices.Add(new Vertex(target));
            PositionToPredecessor[source] = null;

            var initialWavefront = Wavefront.New(0, 360, new Vertex(source), Vertices, 0);
            if (initialWavefront == null)
            {
                return new List<Position>();
            }

            Wavefronts.Add(initialWavefront);

            Log.Init();
            Log.I($"Routing from {source} to {target}");
            Log.I($"Initial wavefront at {initialWavefront.RootVertex.Position}");
            Log.I($"Wavefront vertices {initialWavefront.RelevantVertices.Map(v => v.Position.ToString()).Join(", ")}");

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
            // TODO Use sorted queue (prioroty queue?)
            Wavefronts.Sort((w1, w2) => (int)(w1.DistanceToNextVertex() - w2.DistanceToNextVertex()));
            var wavefront = Wavefronts[0];
            var currentVertex = wavefront.GetNextVertex();

            Log.I(
                $"Process wavefront at {wavefront.RootVertex.Position} from {wavefront.FromAngle}° to {wavefront.ToAngle}°");

            if (currentVertex == null)
            {
                Log.I("No next vertex, remove wavefront");
                // This wavefront doesn't have any events ahead, to we can remove it. 
                Wavefronts.Remove(wavefront);
                return;
            }

            var isCurrentVertexVisible = IsPositionVisible(wavefront.RootVertex.Position, currentVertex.Position);
            var currentVertexHasBeenVisitedBefore = PositionToPredecessor.ContainsKey(currentVertex.Position);
            if (!isCurrentVertexVisible || currentVertexHasBeenVisitedBefore)
            {
                Log.I($"Ignore event at {currentVertex.Position}: " +
                      $"invalid={!isCurrentVertexVisible}, " +
                      $"already visited={currentVertexHasBeenVisitedBefore}");
                wavefront.IgnoreVertex(currentVertex);
                return;
            }

            if (Equals(currentVertex.Position, targetPosition))
            {
                Log.I($"Target reached ({currentVertex.Position})");
                PositionToPredecessor[currentVertex.Position] = wavefront.RootVertex.Position;
                Log.I($"Set predecessor of target to {wavefront.RootVertex.Position}");
                wavefront.RemoveNextVertex();
                Wavefronts.Remove(wavefront);
                return;
            }

            Log.I($"Next vertex at {currentVertex.Position}");

            wavefront.RemoveNextVertex();
            Log.I("Drop vertex from wavefront");

            double angleShadowFrom;
            double angleShadowTo;

            HandleNeighbors(currentVertex, wavefront, out angleShadowFrom, out angleShadowTo,
                out var newWavefrontCreatedAtEventRoot);
            Log.I($"Handled neighbors, shadow from {angleShadowFrom}° to {angleShadowTo}°");

            if (newWavefrontCreatedAtEventRoot)
            {
                PositionToPredecessor[currentVertex.Position] = wavefront.RootVertex.Position;
                Log.I($"Set predecessor of {currentVertex.Position} to {wavefront.RootVertex.Position}");
            }

            if (!Double.IsNaN(angleShadowFrom) && !Double.IsNaN(angleShadowTo))
            {
                Log.I($"Remove old wavefront from {wavefront.FromAngle}° to {wavefront.ToAngle}° and create new ones");
                Wavefronts.Remove(wavefront);
                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    wavefront.FromAngle, angleShadowFrom);
                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    angleShadowTo, wavefront.ToAngle);
            }
        }

        // TODO document rotation idea of this method
        public void HandleNeighbors(Vertex currentVertex, Wavefront wavefront, out double angleShadowFrom,
            out double angleShadowTo, out bool createdWavefrontAtCurrentVertex)
        {
            Log.I($"Process vertex={currentVertex}");

            angleShadowFrom = Double.NaN;
            angleShadowTo = Double.NaN;
            createdWavefrontAtCurrentVertex = false;

            var rightNeighbor = currentVertex.RightNeighbor?.ToPosition() ?? currentVertex.LeftNeighbor?.ToPosition();
            var leftNeighbor = currentVertex.LeftNeighbor?.ToPosition() ?? currentVertex.RightNeighbor?.ToPosition();

            if (rightNeighbor == null && leftNeighbor == null)
            {
                Log.I("Current vertex has no neighbors -> abort");
                return;
            }

            Log.I($"rightNeighbor={rightNeighbor}, leftNeighbor={leftNeighbor}");

            var angleRootToRightNeighbor = rightNeighbor != null
                ? wavefront.RootVertex.Position.GetBearing(rightNeighbor)
                : double.NaN;
            var angleRootToLeftNeighbor = leftNeighbor != null
                ? wavefront.RootVertex.Position.GetBearing(leftNeighbor)
                : double.NaN;
            var angleRootToCurrentVertex = wavefront.RootVertex.Position.GetBearing(currentVertex.Position);

            var angleVertexToRightNeighbor = rightNeighbor != null
                ? currentVertex.Position.GetBearing(rightNeighbor)
                : double.NaN;
            var angleVertexToLeftNeighbor = leftNeighbor != null
                ? currentVertex.Position.GetBearing(leftNeighbor)
                : double.NaN;

            // Rotate such that the current vertex of always north/up of the wavefront root
            var rotationAngle = -angleRootToCurrentVertex;
            Log.I($"Rotate relevant angles by {rotationAngle}°");
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

            Log.I($"Wavefront goes from={angleWavefrontFrom}° to={angleWavefrontTo}°");

            // Wavefront root vertex is the only neighbor aka we reached the end of a line with the wavefront rooted
            // in the second last vertex of that line.
            var wavefrontRootIsSecondLastLineVertex = Equals(rightNeighbor, wavefront.RootVertex.Position) &&
                                                      Equals(leftNeighbor, wavefront.RootVertex.Position);
            if (wavefrontRootIsSecondLastLineVertex)
            {
                Log.I("Wavefront is second last in line string -> Create new wavefront");
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

            Log.I(
                $"New wavefront needed={newWavefrontNeeded} because: neighborWillBeVisitedByWavefront={neighborWillBeVisitedByWavefront}, " +
                $"angleWavefrontFrom={angleWavefrontFrom}°, angleWavefrontTo={angleWavefrontTo}°");

            // Rotate back
            Log.I($"Rotate every angle back by {-rotationAngle}°");
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
                Log.I(
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
                Log.I(
                    $"Left neighbor={leftNeighbor} has been visited casting a shadow from {angleLeftShadowFrom}° to {angleLeftShadowTo}°");
            }

            // When two shadows exist -> merge them because they always touch
            if (!Double.IsNaN(angleRightShadowFrom) && !Double.IsNaN(angleLeftShadowFrom))
            {
                Log.I("There are two shadows -> merge them");
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

                Log.I($"There were two shadows -> merged shadow goes from {angleShadowFrom}° to {angleShadowTo}°");
            }
        }

        public bool AddNewWavefront(List<Vertex> vertices, Vertex root, double distanceToRootFromSource,
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
            Log.I(
                $"Angles for new wavefront (from={fromAngle}°, to={toAngle}°) exceed 0° border? {Angle.IsBetween(fromAngle, 0, toAngle)}");
            if (Angle.IsBetween(fromAngle, 0, toAngle))
            {
                newWavefrontCreated |= AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, 360);
                newWavefrontCreated |= AddWavefrontIfValid(vertices, distanceToRootFromSource, root, 0, toAngle);
            }
            else
            {
                newWavefrontCreated |=
                    AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, toAngle);
            }

            return newWavefrontCreated;
        }

        public bool AddWavefrontIfValid(List<Vertex> relevantVertices, double distanceFromSourceToVertex,
            Vertex rootVertex, double fromAngle, double toAngle)
        {
            var newWavefront = Wavefront.New(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex);
            if (newWavefront != null)
            {
                Log.I(
                    $"New wavefront at {newWavefront.RootVertex.Position} with {newWavefront.RelevantVertices.Count} relevant vertices from {fromAngle}° to {toAngle}°");
                Log.I($"Relevant vertices: {newWavefront.RelevantVertices.Map(v => v.Position.ToString()).Join(", ")}");
                Wavefronts.Add(newWavefront);
                return true;
            }

            Log.I(
                $"New wavefront at {rootVertex.Position} from {fromAngle}° to {toAngle}° wouldn't have any vertices -> ignore it");
            return false;
        }

        private bool IsPositionVisible(Position startPosition, Position endPosition)
        {
            return !TrajectoryCollidesWithObstacle(startPosition.X, startPosition.Y, endPosition.X, endPosition.Y);
        }

        private bool TrajectoryCollidesWithObstacle(double startPositionX, double startPositionY, double endPositionX,
            double endPositionY)
        {
            var lineStringToEvent = new LineString(new[]
            {
                new Coordinate(startPositionX, startPositionY),
                new Coordinate(endPositionX, endPositionY)
            });

            return Obstacles.Any(obstacle => obstacle.Crosses(lineStringToEvent));
        }
    }
}