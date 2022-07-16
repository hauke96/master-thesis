using Mars.Components.Layers;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace RoutingWithLineObstacle.Layer
{
    public class ObstacleLayer : VectorLayer
    {
        public Position NearestVertex(Position position)
        {
            Console.WriteLine($"Calculate nearest vertex to position {position}");

            var lineString = Nearest(position.PositionArray).VectorStructured.Geometry as LineString;
            var coordinates = lineString.Coordinates.Copy();

            coordinates.Each(coordinate =>
                Console.WriteLine(
                    $"  For {coordinate}: {position.DistanceInMTo(Position.CreateGeoPosition(coordinate.X, coordinate.Y))}"));

            Array.Sort(coordinates,
                (c1, c2) =>
                {
                    var distanceInMToC1 = position.DistanceInMTo(Position.CreateGeoPosition(c1.X, c1.Y));
                    var distanceInMToC2 = position.DistanceInMTo(Position.CreateGeoPosition(c2.X, c2.Y));

                    Console.WriteLine($"  Distance: {distanceInMToC1}({c1}) / /{distanceInMToC2}({c2})");

                    return (int)(distanceInMToC1 - distanceInMToC2);
                });

            Console.WriteLine($"  Nearest coordinate: {coordinates[0]}");

            return Position.CreateGeoPosition(coordinates[0].X, coordinates[0].Y);
        }

        /// <summary>
        /// Returns all vertices ordered by distance with the given position as center.
        /// </summary>
        /// <returns>List of vertices ordered by distance. Element 0 is the nearest vertex.</returns>
        public List<Position> SortedVertices(Position position)
        {
            List<Coordinate> coordinates = new List<Coordinate>();

            Features.Each(feature => coordinates.AddRange(feature.VectorStructured.Geometry.Coordinates));

            coordinates.Sort((c1, c2) =>
            {
                var distanceInMToC1 = position.DistanceInMTo(Position.CreateGeoPosition(c1.X, c1.Y));
                var distanceInMToC2 = position.DistanceInMTo(Position.CreateGeoPosition(c2.X, c2.Y));

                Console.WriteLine($"  Distance: {distanceInMToC1}({c1}) / /{distanceInMToC2}({c2})");

                return (int)(distanceInMToC1 - distanceInMToC2);
            });

            return coordinates.Map(c => Position.CreateGeoPosition(c.X, c.Y));
        }
    }
}