using System.Collections.Generic;
using Mars.Common;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using Wavefront.Geometry;

namespace Wavefront.Tests;

public class WavefrontPreprocessorTest
{
    [Test]
    public void GetNeighborsFromObstacleVertices_SimpleLineString()
    {
        var obstacle = new LineString(new[]
        {
            new Coordinate(2, 5),
            new Coordinate(2, 10)
        });
        var positionToNeighbors =
            WavefrontPreprocessor.GetNeighborsFromObstacleVertices(new List<Obstacle> { new(obstacle) });

        Assert.AreEqual(2, positionToNeighbors.Count);

        Assert.AreEqual(1, positionToNeighbors[obstacle[0].ToPosition()].Count);
        Assert.Contains(obstacle[1].ToPosition(), positionToNeighbors[obstacle[0].ToPosition()]);

        Assert.AreEqual(1, positionToNeighbors[obstacle[1].ToPosition()].Count);
        Assert.Contains(obstacle[0].ToPosition(), positionToNeighbors[obstacle[1].ToPosition()]);
    }

    [Test]
    public void GetNeighborsFromObstacleVertices_MultiVertexLineString()
    {
        var obstacle = new LineString(new[]
        {
            new Coordinate(6, 3),
            new Coordinate(7, 3),
            new Coordinate(7, 4),
        });
        var positionToNeighbors =
            WavefrontPreprocessor.GetNeighborsFromObstacleVertices(new List<Obstacle> { new(obstacle) });

        Assert.AreEqual(3, positionToNeighbors.Count);

        Assert.AreEqual(1, positionToNeighbors[obstacle[0].ToPosition()].Count);
        Assert.Contains(obstacle[1].ToPosition(), positionToNeighbors[obstacle[0].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle[1].ToPosition()].Count);
        Assert.Contains(obstacle[2].ToPosition(), positionToNeighbors[obstacle[1].ToPosition()]);
        Assert.Contains(obstacle[0].ToPosition(), positionToNeighbors[obstacle[1].ToPosition()]);

        Assert.AreEqual(1, positionToNeighbors[obstacle[2].ToPosition()].Count);
        Assert.Contains(obstacle[1].ToPosition(), positionToNeighbors[obstacle[2].ToPosition()]);
    }

    [Test]
    public void GetNeighborsFromObstacleVertices_Polygon()
    {
        var obstacle = new LineString(new[]
        {
            new Coordinate(1, 2.5),
            new Coordinate(2.5, 1),
            new Coordinate(3.5, 2),
            new Coordinate(2, 3.5),
            new Coordinate(1, 2.5)
        });

        var positionToNeighbors =
            WavefrontPreprocessor.GetNeighborsFromObstacleVertices(new List<Obstacle> { new(obstacle) });

        Assert.AreEqual(4, positionToNeighbors.Count);

        Assert.AreEqual(2, positionToNeighbors[obstacle[0].ToPosition()].Count);
        Assert.Contains(obstacle[1].ToPosition(), positionToNeighbors[obstacle[0].ToPosition()]);
        Assert.Contains(obstacle[3].ToPosition(), positionToNeighbors[obstacle[0].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle[1].ToPosition()].Count);
        Assert.Contains(obstacle[0].ToPosition(), positionToNeighbors[obstacle[1].ToPosition()]);
        Assert.Contains(obstacle[2].ToPosition(), positionToNeighbors[obstacle[1].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle[2].ToPosition()].Count);
        Assert.Contains(obstacle[1].ToPosition(), positionToNeighbors[obstacle[2].ToPosition()]);
        Assert.Contains(obstacle[3].ToPosition(), positionToNeighbors[obstacle[2].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle[3].ToPosition()].Count);
        Assert.Contains(obstacle[0].ToPosition(), positionToNeighbors[obstacle[3].ToPosition()]);
        Assert.Contains(obstacle[2].ToPosition(), positionToNeighbors[obstacle[3].ToPosition()]);
    }

    [Test]
    public void GetNeighborsFromObstacleVertices_TouchingPolygon()
    {
        var obstacle1 = new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(1, 1),
            new Coordinate(0, 1),
            new Coordinate(0, 0)
        });
        var obstacle2 = new LineString(new[]
        {
            new Coordinate(1, 0),
            new Coordinate(2, 0),
            new Coordinate(2, 1),
            new Coordinate(1, 1),
            new Coordinate(1, 0)
        });

        var list = new List<Obstacle> { new(obstacle1), new(obstacle2) };

        var positionToNeighbors = WavefrontPreprocessor.GetNeighborsFromObstacleVertices(list);

        Assert.AreEqual(6, positionToNeighbors.Count);

        // Neighbors of obstacle1
        Assert.AreEqual(2, positionToNeighbors[obstacle1[0].ToPosition()].Count);
        Assert.Contains(obstacle1[1].ToPosition(), positionToNeighbors[obstacle1[0].ToPosition()]);
        Assert.Contains(obstacle1[3].ToPosition(), positionToNeighbors[obstacle1[0].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[1].ToPosition()].Count);
        Assert.Contains(obstacle1[0].ToPosition(), positionToNeighbors[obstacle1[1].ToPosition()]);
        Assert.Contains(obstacle2[1].ToPosition(), positionToNeighbors[obstacle1[1].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[2].ToPosition()].Count);
        Assert.Contains(obstacle2[2].ToPosition(), positionToNeighbors[obstacle1[2].ToPosition()]);
        Assert.Contains(obstacle1[3].ToPosition(), positionToNeighbors[obstacle1[2].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[3].ToPosition()].Count);
        Assert.Contains(obstacle1[0].ToPosition(), positionToNeighbors[obstacle1[3].ToPosition()]);
        Assert.Contains(obstacle1[2].ToPosition(), positionToNeighbors[obstacle1[3].ToPosition()]);

        // Neighbors of obstacle2
        Assert.AreEqual(2, positionToNeighbors[obstacle2[0].ToPosition()].Count);
        Assert.Contains(obstacle1[0].ToPosition(), positionToNeighbors[obstacle2[0].ToPosition()]);
        Assert.Contains(obstacle2[1].ToPosition(), positionToNeighbors[obstacle2[0].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle2[1].ToPosition()].Count);
        Assert.Contains(obstacle2[0].ToPosition(), positionToNeighbors[obstacle2[1].ToPosition()]);
        Assert.Contains(obstacle2[2].ToPosition(), positionToNeighbors[obstacle2[1].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle2[2].ToPosition()].Count);
        Assert.Contains(obstacle2[3].ToPosition(), positionToNeighbors[obstacle2[2].ToPosition()]);
        Assert.Contains(obstacle2[1].ToPosition(), positionToNeighbors[obstacle2[2].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle2[3].ToPosition()].Count);
        Assert.Contains(obstacle1[3].ToPosition(), positionToNeighbors[obstacle2[3].ToPosition()]);
        Assert.Contains(obstacle2[2].ToPosition(), positionToNeighbors[obstacle2[3].ToPosition()]);
    }
}