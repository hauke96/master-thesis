using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using NetTopologySuite.Geometries;
using NUnit.Framework;

namespace HybridVisibilityGraphRouting.Tests.Graph;

public class HybridVisibilityGraphGeneratorTest
{
    [Test]
    public void GetObstacles()
    {
        var featureObstacle = new VectorFeature
        {
            VectorStructured = new VectorStructuredData
            {
                Geometry = new LineString(new[] { new Coordinate(0, 0), new Coordinate(1, 1) }),
                Data = new Dictionary<string, object> { { "building", "yes" } },
            }
        };
        var featureNonObstacle = new VectorFeature
        {
            VectorStructured = new VectorStructuredData
            {
                Geometry = new LineString(new[] { new Coordinate(0, 2), new Coordinate(1, 3) }),
                Data = new Dictionary<string, object> { { "foo", "bar" } },
            }
        };

        var obstacles = HybridVisibilityGraphGenerator.GetObstacles(new[] { featureObstacle, featureNonObstacle })
            .QueryAll();

        Assert.AreEqual(1, obstacles.Count);
        var obstacle = obstacles[0];
        CollectionAssert.AreEqual(featureObstacle.VectorStructured.Geometry.Coordinates, obstacle.Coordinates);
        Assert.AreEqual(2, obstacle.Vertices.Count);
        Assert.AreEqual(obstacle.Coordinates[0].ToPosition(), obstacle.Vertices[0].Position);
        Assert.AreEqual(obstacle.Coordinates[1].ToPosition(), obstacle.Vertices[1].Position);
    }

    [Test]
    public void GetObstacles_usesTriangulation()
    {
        var featureObstacle = new VectorFeature
        {
            VectorStructured = new VectorStructuredData
            {
                Geometry = new Polygon(
                    new LinearRing(new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(1, 0),
                        new Coordinate(1, 1),
                        new Coordinate(0, 1),
                        new Coordinate(0, 0)
                    })
                ),
                Data = new Dictionary<string, object> { { "building", "yes" } },
            }
        };

        var obstacles = HybridVisibilityGraphGenerator.GetObstacles(new[] { featureObstacle }).QueryAll();

        Assert.AreEqual(2, obstacles.Count);

        var obstacle = obstacles[0];
        Assert.AreEqual(4, obstacle.Coordinates.Count);
        CollectionAssert.IsSupersetOf(featureObstacle.VectorStructured.Geometry.Coordinates.Distinct(),
            obstacle.Coordinates.Distinct());

        obstacle = obstacles[1];
        Assert.AreEqual(4, obstacle.Coordinates.Count);
        CollectionAssert.IsSupersetOf(featureObstacle.VectorStructured.Geometry.Coordinates.Distinct(),
            obstacle.Coordinates.Distinct());
    }

    [Test]
    public void DetermineVisibilityNeighbors()
    {
        var obstacle1 = new Obstacle(new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(2, 0)
        }));
        var obstacle2 = new Obstacle(new LineString(new[]
        {
            new Coordinate(0.5, 1),
            new Coordinate(10, 1)
        }));
        var obstacle3 = new Obstacle(new LineString(new[]
        {
            new Coordinate(0, 2),
            new Coordinate(1, 2),
            new Coordinate(2, 2)
        }));

        var obstacleIndex = new QuadTree<Obstacle>();
        obstacleIndex.Insert(obstacle1.Envelope, obstacle1);
        obstacleIndex.Insert(obstacle2.Envelope, obstacle2);
        obstacleIndex.Insert(obstacle3.Envelope, obstacle3);

        var visibilityNeighbors = HybridVisibilityGraphGenerator.DetermineVisibilityNeighbors(obstacleIndex);

        CollectionAssert.AreEquivalent(VisibilityGraphGenerator.CalculateVisibleKnn(obstacleIndex, 36, 10, true),
            visibilityNeighbors);
    }
}