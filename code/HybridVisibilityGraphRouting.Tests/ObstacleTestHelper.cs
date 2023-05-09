using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using NetTopologySuite.Geometries;
using ServiceStack;

namespace HybridVisibilityGraphRouting.Tests;

public class ObstacleTestHelper
{
    public static Obstacle CreateObstacle(NetTopologySuite.Geometries.Geometry geometry)
    {
        var coordinateToVertex = geometry.Coordinates
            .Distinct()
            .Map(c => new Vertex(c, true));

        return new Obstacle(geometry, geometry, coordinateToVertex);
    }

    public static List<Obstacle> CreateObstacles(params NetTopologySuite.Geometries.Geometry[] geometries)
    {
        var coordinateToVertex = geometries
            .Map(g => g.Coordinates)
            .SelectMany(x => x)
            .Distinct()
            .ToDictionary(c => c, c => new Vertex(c, true));

        return geometries.Map(g => new Obstacle(g, g, g.Coordinates.Map(c => coordinateToVertex[c])));
    }

    public static Vertex VertexAt(List<Vertex> vertices, Coordinate coordinate)
    {
        return vertices.First(v => v.Coordinate.Equals(coordinate));
    }
}