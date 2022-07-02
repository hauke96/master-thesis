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

        private Position Source;
        private Position Target;
        private Queue<VertexEvent> Events;

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
        }

        public List<Position> route(Position source, Position target)
        {
            Source = source;
            Target = target;

            Vertices.Add(new Vertex(source));
            Vertices.Add(new Vertex(target));
            PositionToPredecessor[source] = null;

            Events = new Queue<VertexEvent>(initializeVertexEventsFor(source, 0));

            while (!PositionToPredecessor.ContainsKey(target))
            {
                var currentEvent = Events.Dequeue();
                if (PositionToPredecessor.ContainsKey(currentEvent.Position))
                {
                    continue;
                }

                PositionToPredecessor[currentEvent.Position] = currentEvent.Root;

                // Calculate events for the wavefront starting at currentEvent.Position and add them to the current queue of events
                var events = initializeVertexEventsFor(currentEvent.Position, currentEvent.DistanceFromSource);
                events.AddRange(Events.ToArray());
                events.Sort();
                Events = new Queue<VertexEvent>(events);
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
            var lineStringToEvent = new LineString(new[]
            {
                new Coordinate(rawEvent.Root.X, rawEvent.Root.Y),
                new Coordinate(rawEvent.Position.X, rawEvent.Position.Y)
            });

            var obstacleIntersectsWithLineString = Obstacles.Any(obstacle => obstacle.Crosses(lineStringToEvent));
            return !obstacleIntersectsWithLineString;
        }

        private List<Vertex> getSortedVerticesFor(Position position)
        {
            Vertices.Sort((c1, c2) =>
            {
                var distanceInMToC1 = position.DistanceInMTo(Position.CreateGeoPosition(c1.X, c1.Y));
                var distanceInMToC2 = position.DistanceInMTo(Position.CreateGeoPosition(c2.X, c2.Y));

                Console.WriteLine($"  Distance: {distanceInMToC1}({c1}) / /{distanceInMToC2}({c2})");

                return distanceInMToC1 - distanceInMToC2 > 0
                    ? 1
                    : -1;
            });

            return Vertices;
        }
    }
}