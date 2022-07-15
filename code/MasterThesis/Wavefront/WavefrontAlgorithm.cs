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

            Console.WriteLine($"Routing from {source} to {target}");
            Console.WriteLine($"  Initial wavefront at {initialWavefront.RootVertex.Position}");
            Console.WriteLine(
                $"  Wavefront vertices {initialWavefront.RelevantVertices.Map(v => v.Position.ToString()).Join(", ")}");

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
            Wavefronts.Sort((w1, w2) => w1.DistanceToNextVertex() - w2.DistanceToNextVertex() > 0 ? 1 : -1);
            var wavefront = Wavefronts[0];
            var currentVertex = wavefront.GetNextVertex();

            Console.WriteLine(
                $"Process wavefront at {wavefront.RootVertex.Position} from {wavefront.FromAngle}° to {wavefront.ToAngle}°");

            if (currentVertex == null)
            {
                Console.WriteLine("  No next vertex, remove wavefront");
                // This wavefront doesn't have any events ahead, to we can remove it. 
                Wavefronts.Remove(wavefront);
                return;
            }

            if (!IsEventValid(wavefront.RootVertex.Position, currentVertex.Position)
                || PositionToPredecessor.ContainsKey(currentVertex.Position))
            {
                Console.WriteLine("  Event not valid, ignore vertex");
                wavefront.IgnoreVertex(currentVertex);
                return;
            }

            if (Equals(currentVertex.Position, targetPosition))
            {
                Console.WriteLine($"  Target reached ({currentVertex.Position})");
                PositionToPredecessor[currentVertex.Position] = wavefront.RootVertex.Position;
                Console.WriteLine($"  Set predecessor of target to {wavefront.RootVertex.Position}");
                wavefront.RemoveNextVertex();
                Wavefronts.Remove(wavefront);
                return;
            }

            Console.WriteLine($"  Next vertex at {currentVertex.Position}");

            wavefront.RemoveNextVertex();
            Console.WriteLine("  Drop vertex from wavefront");

            double angleShadowFrom;
            double angleShadowTo;

            HandleNeighbors(currentVertex, wavefront, out angleShadowFrom, out angleShadowTo,
                out var newWavefrontCreatedAtEventRoot);
            Console.WriteLine($"  Handled neighbors, shadow from {angleShadowFrom}° to {angleShadowTo}°");

            if (newWavefrontCreatedAtEventRoot)
            {
                PositionToPredecessor[currentVertex.Position] = wavefront.RootVertex.Position;
                Console.WriteLine($"  Set predecessor of {currentVertex.Position} to {wavefront.RootVertex.Position}");
            }

            if (!Double.IsNaN(angleShadowFrom) && !Double.IsNaN(angleShadowTo))
            {
                Console.WriteLine(
                    $"  Remove old wavefront from {wavefront.FromAngle}° to {wavefront.ToAngle}° and create new ones");
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
            angleShadowFrom = Double.NaN;
            angleShadowTo = Double.NaN;
            createdWavefrontAtCurrentVertex = false;

            var rightNeighbor = currentVertex.RightNeighbor?.ToPosition() ?? currentVertex.LeftNeighbor?.ToPosition();
            var leftNeighbor = currentVertex.LeftNeighbor?.ToPosition() ?? currentVertex.RightNeighbor?.ToPosition();

            if (rightNeighbor == null && leftNeighbor == null)
            {
                Console.WriteLine("  Current vertex has no neighbors");
                return;
            }

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
            var angleCurrentWavefrontFrom = Angle.Normalize(wavefront.FromAngle + rotationAngle);
            var angleCurrentWavefrontTo = Angle.Normalize(wavefront.ToAngle + rotationAngle);
            angleRootToRightNeighbor = Angle.Normalize(angleRootToRightNeighbor + rotationAngle);
            angleRootToLeftNeighbor = Angle.Normalize(angleRootToLeftNeighbor + rotationAngle);
            angleRootToCurrentVertex = Angle.Normalize(angleRootToCurrentVertex + rotationAngle);
            angleVertexToRightNeighbor = Angle.Normalize(angleVertexToRightNeighbor + rotationAngle);
            angleVertexToLeftNeighbor = Angle.Normalize(angleVertexToLeftNeighbor + rotationAngle);

            var rightNeighborHasBeenVisited = wavefront.HasBeenVisited(rightNeighbor);
            var leftNeighborHasBeenVisited = wavefront.HasBeenVisited(leftNeighbor);

            // Both neighbors on left/right (aka west/east) side of root+current vertex -> New wavefront needed for the casted shadow
            var bothNeighborsOnWestSide = angleVertexToRightNeighbor >= 180 && angleVertexToLeftNeighbor >= 180;
            var bothNeighborsOnEastSide = angleVertexToRightNeighbor <= 180 && angleVertexToLeftNeighbor <= 180;

            double angleWavefrontFrom = Double.NaN;
            double angleWavefrontTo = Double.NaN;
            if (bothNeighborsOnWestSide)
            {
                angleWavefrontFrom = Math.Max(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                angleWavefrontTo = 360;

                // angleShadowFrom = Math.Min(angleRootToRightNeighbor, angleRootToLeftNeighbor);
                // angleShadowTo = 360;
            }
            else if (bothNeighborsOnEastSide)
            {
                angleWavefrontFrom = 0;
                angleWavefrontTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);

                // angleShadowFrom = 0;
                // angleShadowTo = Math.Max(angleRootToRightNeighbor, angleRootToLeftNeighbor);
            }

            // Wavefront root vertex if the only neighbor aka we reached the end of a line with the wavefront rooted
            // in the second last vertex of that line.
            var wavefrontRootIsSecondLastLineVertex = Equals(rightNeighbor, wavefront.RootVertex.Position) &&
                                                      Equals(leftNeighbor, wavefront.RootVertex.Position);
            if (wavefrontRootIsSecondLastLineVertex)
            {
                if (Math.Abs(angleCurrentWavefrontTo % 360) < 0.01)
                {
                    angleWavefrontFrom = 0;
                    angleWavefrontTo = Math.Min(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                }
                else if (Math.Abs(angleCurrentWavefrontFrom % 360) < 0.01)
                {
                    angleWavefrontFrom = Math.Max(angleVertexToRightNeighbor, angleVertexToLeftNeighbor);
                    angleWavefrontTo = 360;
                }
            }

            var neighborWillBeVisitedByWavefront = wavefrontRootIsSecondLastLineVertex &&
                                                   Angle.IsBetween(wavefront.FromAngle, angleWavefrontFrom,
                                                       wavefront.ToAngle);
            var newWavefrontNeeded = !double.IsNaN(angleWavefrontFrom) && !double.IsNaN(angleWavefrontTo) &&
                                     !neighborWillBeVisitedByWavefront;

            // Rotate back
            angleRootToRightNeighbor = Angle.Normalize(angleRootToRightNeighbor - rotationAngle);
            angleRootToLeftNeighbor = Angle.Normalize(angleRootToLeftNeighbor - rotationAngle);
            angleRootToCurrentVertex = Angle.Normalize(angleRootToCurrentVertex - rotationAngle);
            angleWavefrontFrom = Angle.Normalize(angleWavefrontFrom - rotationAngle);
            angleWavefrontTo = Angle.Normalize(angleWavefrontTo - rotationAngle);

            if (newWavefrontNeeded)
            {
                createdWavefrontAtCurrentVertex = AddNewWavefront(Vertices, currentVertex,
                    wavefront.DistanceTo(currentVertex.Position), angleWavefrontFrom, angleWavefrontTo);
            }

            double angleRightShadowFrom = Double.NaN;
            double angleRightShadowTo = Double.NaN;
            if (wavefront.HasBeenVisited(rightNeighbor))
            {
                Angle.GetEnclosingAngles(angleRootToRightNeighbor, angleRootToCurrentVertex, out angleRightShadowFrom,
                    out angleRightShadowTo);
                angleShadowFrom = angleRightShadowFrom;
                angleShadowTo = angleRightShadowTo;
                Console.WriteLine(
                    $"  Right neighbor={rightNeighbor}, visited={rightNeighborHasBeenVisited} casting a shadow from {angleShadowFrom}° to {angleShadowTo}°");
            }

            double angleLeftShadowFrom = Double.NaN;
            double angleLeftShadowTo = Double.NaN;
            if (wavefront.HasBeenVisited(leftNeighbor))
            {
                Angle.GetEnclosingAngles(angleRootToLeftNeighbor, angleRootToCurrentVertex, out angleLeftShadowFrom,
                    out angleLeftShadowTo);
                angleShadowFrom = angleLeftShadowFrom;
                angleShadowTo = angleLeftShadowTo;
                Console.WriteLine(
                    $"  Left neighbor={leftNeighbor}, visited={leftNeighborHasBeenVisited} casting a shadow from {angleLeftShadowFrom}° to {angleLeftShadowTo}°");
            }

            // When two shadows exist -> merge them because they always touch
            if (!Double.IsNaN(angleRightShadowFrom) && !Double.IsNaN(angleLeftShadowFrom))
            {
                Console.WriteLine($"  There are two shadows -> merge them");
                if (Math.Abs(Angle.Difference(angleRightShadowTo, angleLeftShadowFrom)) < 0.01)
                {
                    angleShadowFrom = angleRightShadowFrom;
                    angleShadowTo = angleLeftShadowTo;
                }
                else
                {
                    angleShadowFrom = angleLeftShadowFrom;
                    angleShadowTo = angleRightShadowTo;
                }

                Console.WriteLine(
                    $"  There were two shadows -> merged shadow goes from {angleShadowFrom}° to {angleShadowTo}°");
            }
        }

        public bool AddWavefrontIfValid(List<Vertex> relevantVertices, double distanceFromSourceToVertex,
            Vertex rootVertex, double fromAngle, double toAngle)
        {
            // Ignore angles that are nearly the same
            const double floatRoundingTolerance = 0.0001;
            if (Math.Abs(fromAngle % 360 - toAngle % 360) < floatRoundingTolerance)
            {
                return false;
            }

            toAngle = toAngle == 0 ? 360 : toAngle;
            var newWavefront = Wavefront.New(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex);
            if (newWavefront != null)
            {
                Console.WriteLine(
                    $"    New wavefront at {newWavefront.RootVertex.Position} with {newWavefront.RelevantVertices.Count} relevant vertices from {fromAngle}° to {toAngle}°");
                Console.WriteLine(
                    $"      Relevant vertices: {newWavefront.RelevantVertices.Map(v => v.Position.ToString()).Join(", ")}");
                Wavefronts.Add(newWavefront);
                return true;
            }

            Console.WriteLine(
                $"    New wavefront at {rootVertex.Position} from {fromAngle}° to {toAngle}° wouldn't have any vertices -> ignore it");
            return false;
        }

        public bool AddNewWavefront(List<Vertex> vertices, Vertex root, double distanceToRootFromSource,
            double fromAngle, double toAngle)
        {
            bool newWavefrontCreated = false;
            /*
             * If the interesting area exceeds the 0° border (e.g. goes from 300° via 0° to 40°), then we remove the
             * old wavefront and create two new ones. One from 300° to 360° and one from 0° to 40°. This simply
             * makes range checks easier and has no further reason.
             */
            if (fromAngle > toAngle)
            {
                Console.WriteLine("    Angles for new wavefront exceed 0° border -> Create two");
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

        private bool IsEventValid(Position eventRootPosition, Position eventPosition)
        {
            return IsPositionVisible(eventRootPosition, eventPosition);
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