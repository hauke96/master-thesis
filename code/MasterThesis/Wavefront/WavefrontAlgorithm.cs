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
                var rightNeighborVisible = TrajectoryCollidesWithObstacle(wavefront.RootVertex.X,
                    wavefront.RootVertex.Y, rightNeighbor.X,
                    rightNeighbor.Y);
                var rightNeighborToWavefrontRootAngle =
                    wavefront.RootVertex.Position.GetBearing(Position.CreateGeoPosition(rightNeighbor.X,
                        rightNeighbor.Y));
                var rightAngle = rightNeighborToWavefrontRootAngle;
                if (!rightNeighborVisible)
                {
                    // The right neighbor is not visible -> The angle wavefront based on the wavefront.RootVertex only
                    // goes to the angle between wavefront root and the current vertex
                    rightAngle = currentVertex.Position.GetBearing(wavefront.RootVertex.Position);

                    // TODO new wavefront with rightNeighbor as root and appropriate angle
                    // TODO Add new wavefront to list of wavefronts
                }

                var leftNeighbor = currentVertex.LeftNeighbor;
                var leftNeighborVisible = TrajectoryCollidesWithObstacle(wavefront.RootVertex.X, wavefront.RootVertex.Y,
                    leftNeighbor.X,
                    leftNeighbor.Y);
                var leftNeighborToWavefrontRootAngle =
                    wavefront.RootVertex.Position.GetBearing(Position.CreateGeoPosition(leftNeighbor.X,
                        leftNeighbor.Y));
                var leftAngle = leftNeighborToWavefrontRootAngle;
                if (!leftNeighborVisible)
                {
                    // The right neighbor is not visible -> The angle wavefront based on the wavefront.RootVertex only
                    // goes to the angle between wavefront root and the current vertex
                    leftAngle = currentVertex.Position.GetBearing(wavefront.RootVertex.Position);

                    // TODO new wavefront with Neighbor as root and appropriate angle
                    // TODO Add new wavefront to list of wavefronts
                }

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
                var fromCoordinate = leftNeighbor;
                var fromAngle = leftAngle;
                var toCoordinate = rightNeighbor;
                var toAngle = rightAngle;

                // When angleDiff < 180 then the situation is switched and the wanted area for the wavefront is from
                // the right neighbor to the left neighbor.
                if (angleDiff < 180)
                {
                    fromCoordinate = rightNeighbor;
                    fromAngle = rightAngle;
                    toCoordinate = leftNeighbor;
                    toAngle = leftAngle;
                }

                // TODO When 0° is between fromAngle and toAngle -> Split wavefront into to: fromAngle -> 0° and 0° -> toAngle
                // TODO Add new wavefront to list of wavefronts
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