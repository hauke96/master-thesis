using Mars.Common;
using Mars.Common.Core.Collections;
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
        private readonly List<Vertex> Vertices;
        private readonly Dictionary<Position, Position?> PositionToPredecessor;

        private List<Wavefront> Wavefronts;

        public WavefrontAlgorithm(List<NetTopologySuite.Geometries.Geometry> obstacles)
        {
            Obstacles = obstacles;
            Vertices = new List<Vertex>();
            PositionToPredecessor = new Dictionary<Position, Position?>();

            Obstacles.Each(obstacle =>
            {
                var positions = obstacle.Coordinates.Map(c => new Vertex(c, obstacle));
                Vertices.AddRange(positions);
            });
            Wavefronts = new List<Wavefront>();
        }

        public List<Position> route(Position source, Position target)
        {
            Vertices.Add(new Vertex(source));
            Vertices.Add(new Vertex(target));
            PositionToPredecessor[source] = null;

            while (!PositionToPredecessor.ContainsKey(target))
            {
                // TODO Use sorted queue (prioroty queue?)
                Wavefronts.Sort((w1, w2) => w1.DistanceToNextVertex() - w2.DistanceToNextVertex() > 0 ? 1 : -1);
                var wavefront = Wavefronts[0];
                var currentVertex = wavefront.GetNextVertex();

                if (!IsEventValid(wavefront.RootVertex.Position, currentVertex.Position)
                    || PositionToPredecessor.ContainsKey(currentVertex.Position))
                {
                    wavefront.IgnoreVertex(currentVertex);
                    continue;
                }

                var currentEvent = new VertexEvent(currentVertex, wavefront.RootVertex.Position,
                    wavefront.DistanceToRootFromSource);

                PositionToPredecessor[currentEvent.Position] = currentEvent.Root;

                var rightNeighbor = currentVertex.RightNeighbor;
                var angleWavefrontRight = HandleNeighborVertex(wavefront, rightNeighbor, currentVertex, currentEvent);

                var leftNeighbor = currentVertex.LeftNeighbor;
                var angleWavefrontLeft = HandleNeighborVertex(wavefront, leftNeighbor, currentVertex, currentEvent);

                AdjustWavefront(wavefront, angleWavefrontRight, angleWavefrontLeft);
            }

            var waypoints = new List<Position>();
            var nextPosition = target;

            while (nextPosition != null)
            {
                waypoints.Add(nextPosition);
                nextPosition = PositionToPredecessor[nextPosition];
            }

            waypoints.Reverse();
            return waypoints;
        }

        /// <summary>
        /// Handles the given neighbor vertex of currentVertex. This means a new wavefront might be spawned if the
        /// neighbor is not visible from the root vertex of the given wavefront.
        /// </summary>
        /// <returns>
        /// The angle that should be used to adjust the given wavefront. This is basically the right/left edge
        /// of the "shadow" casted by the wavefront and the line-part between left and right neighbor of the event.
        /// </returns>
        private double HandleNeighborVertex(Wavefront wavefront, Coordinate neighbor, Vertex currentVertex,
            VertexEvent currentEvent)
        {
            var neighborVisible = TrajectoryCollidesWithObstacle(wavefront.RootVertex.X,
                wavefront.RootVertex.Y, neighbor.X,
                neighbor.Y);
            var angleWavefrontRootToNeighbor =
                wavefront.RootVertex.Position.GetBearing(Position.CreateGeoPosition(neighbor.X,
                    neighbor.Y));
            var angleNewWavefrontEdge = angleWavefrontRootToNeighbor;
            if (!neighborVisible)
            {
                // The neighbor is not visible -> The angle wavefront based on the wavefront.RootVertex only
                // goes to the angle between wavefront root and the current vertex
                angleNewWavefrontEdge = wavefront.RootVertex.Position.GetBearing(currentVertex.Position);
                var angleEventRootToNeighbor =
                    currentVertex.Position.GetBearing(new Position(neighbor.X, neighbor.Y));

                var fromAngle = Math.Min(angleEventRootToNeighbor, angleNewWavefrontEdge);
                var toAngle = Math.Max(angleEventRootToNeighbor, angleNewWavefrontEdge);

                Wavefronts.Add(new Wavefront(fromAngle, toAngle, currentEvent.Vertex, wavefront.RelevantVertices,
                    wavefront.DistanceToRootFromSource));
            }

            return angleNewWavefrontEdge;
        }

        private void AdjustWavefront(Wavefront oldWavefront, double rightAngle, double leftAngle)
        {
            /*
                 *              r
                 *  * -------- *
                 *   \         |
                 *    \        |        X source
                 *     * ----- *
                 *    l         v
                 *
                 * Vertices r and v are visible from the source X. Let r be the right neighbor and l be the left.
                 * 
                 * This means "leftAngle" is the same as the angle from X to v and right angle is the angle from
                 * X to r. Let's say leftAngle is 250 and rightAngle is 300. The angleDiff is then
                 *
                 *     (300 - 250 + 360) % 360
                 *   = (50 + 360) % 360
                 *   = 410 % 360
                 *   = 50
                 *
                 * So angleDiff < 180 and this means the angle of the wavefront rooted in X should now be between
                 * rightAngle and leftAngle, so the imaginary arc should go from r to l (clockwise). 
                 */

            // When angleDiff > 180 then the wanted range of the resulting wavefront is from left neighbor to
            // the right neighbor.
            var angleDiff = (rightAngle - leftAngle + 360) % 360;
            var fromAngle = leftAngle;
            var toAngle = rightAngle;

            // When angleDiff < 180 then the situation is switched and the wanted area for the wavefront is from
            // the right neighbor to the left neighbor.
            if (angleDiff < 180)
            {
                fromAngle = rightAngle;
                toAngle = leftAngle;
            }

            /*
                 * If the interesting area exceeds the 0° border (e.g. goes from 300° via 0° to 40°), then we remove the
                 * old wavefront and create two new ones. One from 300° to 360° and one from 0° to 40°. This simply
                 * makes range checks easier and has no further reason.
                 */
            if (fromAngle > toAngle)
            {
                Wavefronts.Remove(oldWavefront);
                Wavefronts.Add(
                    new Wavefront(fromAngle, 360, oldWavefront.RootVertex, oldWavefront.RelevantVertices,
                        oldWavefront.DistanceToRootFromSource));
                Wavefronts.Add(
                    new Wavefront(0, toAngle, oldWavefront.RootVertex, oldWavefront.RelevantVertices,
                        oldWavefront.DistanceToRootFromSource));
            }
            else
            {
                oldWavefront.SetAngles(fromAngle, toAngle);
            }
        }

        private List<VertexEvent> initializeVertexEventsFor(Position eventRoot, double distanceToRootFromSource)
        {
            var vertexEvents = getSortedVerticesFor(eventRoot)
                .Map(vertexPosition =>
                    new VertexEvent(vertexPosition, eventRoot, distanceToRootFromSource))
                .FindAll(isEventValid);
            vertexEvents.Sort();
            return vertexEvents;
        }

        private bool isEventValid(VertexEvent rawEvent)
        {
            var eventRootPosition = rawEvent.Root;
            var eventPosition = rawEvent.Position;

            return IsEventValid(eventRootPosition, eventPosition);
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

        private List<Vertex> getSortedVerticesFor(Position position)
        {
            return Vertices;
        }
    }
}