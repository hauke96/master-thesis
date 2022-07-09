using Mars.Common;
using Mars.Common.Core;
using Mars.Interfaces.Environments;
using NetTopologySuite.Geometries;
using RoutingWithLineObstacle.Wavefront.Events;
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

        public List<Position> route(Position source, Position target)
        {
            Vertices.Add(new Vertex(target));
            PositionToPredecessor[source] = null;

            var initialWavefront = Wavefront.newIfValid(0, 360, new Vertex(source), Vertices, 0);
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
                ProcessNextEvent();
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

        public void ProcessNextEvent()
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

            Console.WriteLine($"  Next vertex at {currentVertex.Position}");

            if (!IsEventValid(wavefront.RootVertex.Position, currentVertex.Position)
                || PositionToPredecessor.ContainsKey(currentVertex.Position))
            {
                Console.WriteLine("  Event not valid, ignore vertex");
                wavefront.IgnoreVertex(currentVertex);
                return;
            }

            wavefront.RemoveNextVertex();
            Console.WriteLine("  Drop vertex from wavefront");

            // TODO Need the event here? Probably want to keep if for more complex situations later?
            var currentEvent = new VertexEvent(currentVertex, wavefront.RootVertex.Position,
                wavefront.DistanceToRootFromSource);

            PositionToPredecessor[currentEvent.Position] = currentEvent.Root;
            Console.WriteLine($"  Set predecessor of {currentEvent.Position} to {currentEvent.Root}");

            double angleWavefrontRight;
            double angleWavefrontLeft;

            try
            {
                HandleNeighbors(currentVertex, wavefront, out angleWavefrontRight, out angleWavefrontLeft);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            Console.WriteLine($"  Handled neighbors: right={angleWavefrontRight}, left={angleWavefrontLeft}");

            // var angleWavefrontRootToCurrentVertex =
            //     wavefront.RootVertex.Position.GetBearing(currentVertex.Position);
            //
            // double fromAngle;
            // double toAngle;
            // //
            // // // We need to create a new wavefront starting at the end of the line we found
            // // if (IsEndOfGeometry(rightNeighbor, leftNeighbor))
            // // {
            // //     var neighbor = rightNeighbor ?? leftNeighbor;
            // //     var angleCurrentVertexToNeighbor =
            // //         currentVertex.Position.GetBearing(new Position(neighbor.X, neighbor.Y));
            // //
            // //     var distanceFromSourceToCurrentVertex = wavefront.DistanceToRootFromSource +
            // //                                             wavefront.RootVertex.Position.DistanceInMTo(
            // //                                                 currentVertex.Position);
            // //
            // //     GetEnclosingAngles(angleWavefrontRootToCurrentVertex, angleCurrentVertexToNeighbor, out toAngle,
            // //         out fromAngle);
            // //
            // //     // Not the relevant vertices from the wavefront as we create a new wavefront in a whole new direction.
            // //     AdjustWavefront(Vertices, currentVertex, distanceFromSourceToCurrentVertex, fromAngle, toAngle);
            // // }
            //
            // if (Angle.IsBetween(angleWavefrontRight, angleWavefrontRootToCurrentVertex, angleWavefrontLeft))
            // {
            //     fromAngle = angleWavefrontLeft;
            //     toAngle = angleWavefrontRight;
            // }
            // else
            // {
            //     fromAngle = angleWavefrontRight;
            //     toAngle = angleWavefrontLeft;
            // }
            //
            // AdjustWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
            //     fromAngle, toAngle, wavefront);

            Console.WriteLine($"  Create Wavefront at right angle?");
            Console.WriteLine(
                $"    from={wavefront.FromAngle} < right={angleWavefrontRight}? {wavefront.FromAngle < angleWavefrontRight}");
            Console.WriteLine(
                $"    from={wavefront.FromAngle} <= right={angleWavefrontRight} <= to={wavefront.ToAngle}? {Angle.IsBetween(wavefront.FromAngle, angleWavefrontRight, wavefront.ToAngle)}");
            if (wavefront.FromAngle < angleWavefrontRight &&
                Angle.IsBetween(wavefront.FromAngle, angleWavefrontRight, wavefront.ToAngle))
            {
                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    wavefront.FromAngle, angleWavefrontRight);
            }

            Console.WriteLine($"  Create Wavefront at left angle?");
            Console.WriteLine(
                $"    left={angleWavefrontLeft} < to={wavefront.ToAngle}? {angleWavefrontLeft < wavefront.ToAngle}");
            Console.WriteLine(
                $"    from={wavefront.FromAngle} <= left={angleWavefrontLeft} <= to={wavefront.ToAngle}? {Angle.IsBetween(wavefront.FromAngle, angleWavefrontLeft, wavefront.ToAngle)}");
            if (angleWavefrontLeft < wavefront.ToAngle &&
                Angle.IsBetween(wavefront.FromAngle, angleWavefrontLeft, wavefront.ToAngle))
            {
                AddNewWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    angleWavefrontLeft, wavefront.ToAngle);
            }

            Wavefronts.Remove(wavefront);
        }

        public void HandleNeighbors(Vertex currentVertex, Wavefront wavefront, out double angleWavefrontRight,
            out double angleWavefrontLeft)
        {
            var rightNeighbor = currentVertex.RightNeighbor;
            angleWavefrontRight = HandleNeighborVertex(wavefront, rightNeighbor, currentVertex);

            var leftNeighbor = currentVertex.LeftNeighbor;
            angleWavefrontLeft = HandleNeighborVertex(wavefront, leftNeighbor, currentVertex);

            Angle.GetEnclosingAngles(angleWavefrontRight, angleWavefrontLeft, out angleWavefrontRight,
                out angleWavefrontLeft);
        }

        /// <summary>
        /// Checks if one of the neighbors we found marks the end of the geometry (e.g. the LineString). 
        /// </summary>
        public static bool IsEndOfGeometry(Coordinate? rightNeighbor, Coordinate? leftNeighbor)
        {
            return !(rightNeighbor == null && leftNeighbor == null) && (rightNeighbor == null || leftNeighbor == null);
        }

        /// <summary>
        /// Handles the given neighbor vertex of currentVertex. This means a Wavefront.of might be spawned if the
        /// neighbor is not visible from the root vertex of the given wavefront.
        /// </summary>
        /// <returns>
        /// The angle that should be used to adjust the given wavefront. This is basically the right/left edge
        /// of the "shadow" casted by the wavefront and the line-part between left and right neighbor of the event.
        /// </returns>
        public double HandleNeighborVertex(Wavefront wavefront, Coordinate? neighbor, Vertex currentVertex)
        {
            if (neighbor == null)
            {
                // return wavefront.RootVertex.Position.GetBearing(currentVertex.Position);
                neighbor = currentVertex.RightNeighbor ?? currentVertex.LeftNeighbor;

                // We don't care is the neighbor is visible or not, we want to create a new wavefront, because the
                // edge we're looking at is casting a "shadow".
            }
            else
            {
                // Check if neighbor is visible
                if (!TrajectoryCollidesWithObstacle(wavefront.RootVertex.X, wavefront.RootVertex.Y, neighbor.X,
                        neighbor.Y))
                {
                    // We can directly return the angle from wavefront root to the neighbor because we don't need to create new wavefronts.
                    return wavefront.RootVertex.Position.GetBearing(Position.CreateGeoPosition(neighbor.X, neighbor.Y));
                }
            }

            if (neighbor == null)
            {
                // TODO is this correct?
                return wavefront.RootVertex.Position.GetBearing(currentVertex.Position);
            }

            // TODO assert that neighbor is not visible?
            // TODO assert neighbor not null?

            // The neighbor is not visible -> The angle of the new wavefront based on the wavefront.RootVertex only
            // goes to the angle of the current vertex (seen from the root vertex of the wavefront).
            var angleNewWavefrontEdge = wavefront.RootVertex.Position.GetBearing(currentVertex.Position);
            var angleEventRootToNeighbor = currentVertex.Position.GetBearing(new Position(neighbor.X, neighbor.Y));

            double fromAngle;
            double toAngle;
            Angle.GetEnclosingAngles(angleEventRootToNeighbor, angleNewWavefrontEdge, out fromAngle, out toAngle);

            AddNewWavefront(Vertices, currentVertex, wavefront.DistanceToRootFromSource, fromAngle, toAngle);
            // AddWavefrontIfValid(wavefront.RelevantVertices, wavefront.DistanceToRootFromSource, currentVertex,
            // fromAngle, toAngle);

            return angleNewWavefrontEdge;
        }

        public void AddWavefrontIfValid(List<Vertex> relevantVertices, double distanceFromSourceToVertex,
            Vertex rootVertex, double fromAngle, double toAngle)
        {
            // Ignore angles that are nearly the same
            const double floatRoundingTolerance = 0.0001;
            if (Math.Abs(fromAngle % 360 - toAngle % 360) < floatRoundingTolerance)
            {
                return;
            }

            toAngle = toAngle == 0 ? 360 : toAngle;
            var newWavefront = Wavefront.newIfValid(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex);
            if (newWavefront != null)
            {
                Console.WriteLine(
                    $"    New wavefront at {newWavefront.RootVertex.Position} with {relevantVertices.Count} relevant vertices from {fromAngle}° to {toAngle}°");
                Wavefronts.Add(newWavefront);
            }
            else
            {
                Console.WriteLine(
                    $"    New wavefront at {rootVertex.Position} from {fromAngle}° to {toAngle}° wouldn't have any vertices -> ignore it");
            }
        }

        /// <summary>
        /// Creates one to two new wavefronts depending on the parameters (i.e. if from and to angle exceed the 0°
        /// border). When a wavefront is passed, it'll be removed from the list of wavefronts as a new one will be created.
        /// </summary>
        public void AdjustWavefront(List<Vertex> vertices, Vertex root, double distanceToRootFromSource,
            double fromAngle, double toAngle, Wavefront? wavefront = null)
        {
            if (wavefront != null)
            {
                Wavefronts.Remove(wavefront);
            }

            AddNewWavefront(vertices, root, distanceToRootFromSource, fromAngle, toAngle);
        }

        public void AddNewWavefront(List<Vertex> vertices, Vertex root, double distanceToRootFromSource,
            double fromAngle, double toAngle)
        {
            /*
             * If the interesting area exceeds the 0° border (e.g. goes from 300° via 0° to 40°), then we remove the
             * old wavefront and create two new ones. One from 300° to 360° and one from 0° to 40°. This simply
             * makes range checks easier and has no further reason.
             */
            if (fromAngle > toAngle)
            {
                Console.WriteLine("    Angles for new wavefront exceed 0° border -> Create two");
                AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, 360);
                AddWavefrontIfValid(vertices, distanceToRootFromSource, root, 0, toAngle);
            }
            else
            {
                AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, toAngle);
            }
        }

        private bool IsEventValid(Position eventRootPosition, Position eventPosition)
        {
            return !TrajectoryCollidesWithObstacle(eventRootPosition, eventPosition);
        }

        private bool TrajectoryCollidesWithObstacle(Position startPosition, Position endPosition)
        {
            return TrajectoryCollidesWithObstacle(startPosition.X, startPosition.Y, endPosition.X, endPosition.Y);
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