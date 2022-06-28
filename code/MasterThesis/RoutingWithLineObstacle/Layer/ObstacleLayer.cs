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
            
            coordinates.Each(coordinate => Console.WriteLine($"  For {coordinate}: {position.DistanceInMTo(new Position(coordinate.X, coordinate.Y))}"));
            
            Array.Sort(coordinates,
                (c1, c2) =>
                {
                    // TODO Is this ternary operator correct?
                    var distanceInMToC1 = position.DistanceInMTo(new Position(c1.X, c1.Y));
                    var distanceInMToC2 = position.DistanceInMTo(new Position(c2.X, c2.Y));
                    
                    Console.WriteLine($"  Distance: {distanceInMToC1}({c1}) / /{distanceInMToC2}({c2})");
                    
                    return distanceInMToC1 - distanceInMToC2 > 0
                        ? 1
                        : -1;
                });
            
            Console.WriteLine($"  Nearest coordinate: {coordinates[0]}");
            
            return new Position(coordinates[0].X, coordinates[0].Y);
        }
    }
}