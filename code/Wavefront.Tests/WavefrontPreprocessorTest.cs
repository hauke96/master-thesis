using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Common.Collections;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Tests;

public class WavefrontPreprocessorTest
{
    public class WithMultipleObstacles
    {
        private QuadTree<Obstacle> obstacleQuadTree;
        private LineString multiVertexLineObstacle;
        private LineString rotatedLineObstacle;
        private Dictionary<Position, List<Position>> positionToNeighbors;

        [SetUp]
        public void Setup()
        {
            multiVertexLineObstacle = new LineString(new[]
            {
                new Coordinate(2, 2),
                new Coordinate(3, 2),
                new Coordinate(3, 5)
            });
            rotatedLineObstacle = new LineString(new[]
            {
                new Coordinate(5, 3),
                new Coordinate(6.5, 3.5),
                new Coordinate(7.5, 0.5)
            });
            var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
            obstacleGeometries.Add(multiVertexLineObstacle);
            obstacleGeometries.Add(rotatedLineObstacle);

            var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

            positionToNeighbors = WavefrontPreprocessor.GetNeighborsFromObstacleVertices(obstacles);

            obstacleQuadTree = new QuadTree<Obstacle>();
            obstacles.Each(obstacle => obstacleQuadTree.Insert(obstacle.Envelope, obstacle));
        }

        [Test]
        public void PositionToNeighbors_MultiVertexLineObstacle()
        {
            AssertPositionToNeighbors(multiVertexLineObstacle);
        }

        [Test]
        public void PositionToNeighbors_RotatedLineObstacle()
        {
            AssertPositionToNeighbors(rotatedLineObstacle);
        }

        private void AssertPositionToNeighbors(LineString obstacle)
        {
            var expected = new List<Position> { obstacle[1].ToPosition() };
            var actual = positionToNeighbors[obstacle[0].ToPosition()];
            Assert.AreEqual(expected, actual);

            expected = new List<Position> { obstacle[0].ToPosition(), obstacle[2].ToPosition() };
            actual = positionToNeighbors[obstacle[1].ToPosition()];
            CollectionAssert.AreEquivalent(expected, actual);

            expected = new List<Position> { obstacle[1].ToPosition() };
            actual = positionToNeighbors[obstacle[2].ToPosition()];
            Assert.AreEqual(expected, actual);
        }
    }

    public class WithSimpleObstacle
    {
        private QuadTree<Obstacle> obstacleQuadTree;
        private List<Vertex> vertices;
        private Obstacle obstacle;

        [SetUp]
        public void Setup()
        {
            obstacle = new Obstacle(new LineString(new[]
            {
                new Coordinate(1, 1),
                new Coordinate(1, 2),
                new Coordinate(2, 3)
            }));
            vertices = obstacle.Coordinates.Map(c => new Vertex(c.ToPosition()));

            obstacleQuadTree = new QuadTree<Obstacle>();
            obstacleQuadTree.Insert(obstacle.Envelope, obstacle);
        }

        [Test]
        public void CalculateVisibleKnn()
        {
            var visibleKnn = WavefrontPreprocessor.CalculateVisibleKnn(obstacleQuadTree, 100);

            Assert.AreEqual(2, visibleKnn[vertices[0]].Count);
            Assert.Contains(vertices[1], visibleKnn[vertices[0]]);
            Assert.Contains(vertices[2], visibleKnn[vertices[0]]);

            Assert.AreEqual(2, visibleKnn[vertices[1]].Count);
            Assert.Contains(vertices[0], visibleKnn[vertices[1]]);
            Assert.Contains(vertices[2], visibleKnn[vertices[1]]);

            Assert.AreEqual(2, visibleKnn[vertices[2]].Count);
            Assert.Contains(vertices[0], visibleKnn[vertices[2]]);
            Assert.Contains(vertices[1], visibleKnn[vertices[2]]);
        }
    }

    public class WithAlignedObstacle
    {
        private QuadTree<Obstacle> obstacleQuadTree;
        private List<Vertex> vertices;
        private List<Obstacle> obstacles;

        [SetUp]
        public void Setup()
        {
            var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
            obstacleGeometries.Add(new LineString(new[]
            {
                new Coordinate(1, 1),
                new Coordinate(2, 1),
                new Coordinate(2, 2),
                new Coordinate(1, 2),
                new Coordinate(1, 1)
            })); // -> square
            obstacleGeometries.Add(new LineString(new[]
            {
                new Coordinate(3, 1),
                new Coordinate(3, 2),
                new Coordinate(4, 2)
            })); // -> "r"
            obstacleGeometries.Add(new LineString(new[]
            {
                new Coordinate(5, 2),
                new Coordinate(5, 1),
                new Coordinate(6, 1),
                new Coordinate(6, 2),
            })); // -> "u"

            obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));
            vertices = obstacles.SelectMany(o => o.Coordinates)
                .Map(c => new Vertex(c.ToPosition()))
                .Distinct()
                .ToList();
            ;

            obstacleQuadTree = new QuadTree<Obstacle>();
            obstacles.Each(o => obstacleQuadTree.Insert(o.Envelope, o));
        }

        [Test]
        public void CalculateVisibleKnn()
        {
            var visibleKnn = WavefrontPreprocessor.CalculateVisibleKnn(obstacleQuadTree, 100);

            // vertices[0] = lower left of square 
            var actualCoordinates = visibleKnn[vertices[0]].Map(v => v.Coordinate);
            var expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[1],
                obstacles[0].Coordinates[3]
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

            // vertices[1] = lower right of square
            actualCoordinates = visibleKnn[vertices[1]].Map(v => v.Coordinate);
            expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[0],
                obstacles[0].Coordinates[2],
                obstacles[1].Coordinates[0],
                obstacles[1].Coordinates[1],
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

            // vertices[2] = upper right of square
            actualCoordinates = visibleKnn[vertices[2]].Map(v => v.Coordinate);
            expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[1],
                obstacles[0].Coordinates[3],
                obstacles[1].Coordinates[0],
                obstacles[1].Coordinates[1],
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

            // vertices[3] = upper left of square
            actualCoordinates = visibleKnn[vertices[3]].Map(v => v.Coordinate);
            expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[0],
                obstacles[0].Coordinates[2],
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);
        }
    }

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

    [Test]
    public void GetNeighborsFromObstacleVertices_TouchingLines()
    {
        var obstacle1 = new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 1),
            new Coordinate(0, 2),
            new Coordinate(1, 3)
        });
        var obstacle2 = new LineString(new[]
        {
            new Coordinate(0, 1),
            new Coordinate(0, 2),
            new Coordinate(0, 3)
        });

        var list = new List<Obstacle> { new(obstacle1), new(obstacle2) };

        var positionToNeighbors = WavefrontPreprocessor.GetNeighborsFromObstacleVertices(list);

        Assert.AreEqual(5, positionToNeighbors.Count);

        // Neighbors of obstacle1
        Assert.AreEqual(1, positionToNeighbors[obstacle1[0].ToPosition()].Count);
        Assert.Contains(obstacle1[1].ToPosition(), positionToNeighbors[obstacle1[0].ToPosition()]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[1].ToPosition()].Count);
        Assert.Contains(obstacle1[0].ToPosition(), positionToNeighbors[obstacle1[1].ToPosition()]);
        Assert.Contains(obstacle1[2].ToPosition(), positionToNeighbors[obstacle1[1].ToPosition()]);

        Assert.AreEqual(3, positionToNeighbors[obstacle1[2].ToPosition()].Count);
        Assert.Contains(obstacle1[1].ToPosition(), positionToNeighbors[obstacle1[2].ToPosition()]);
        Assert.Contains(obstacle1[3].ToPosition(), positionToNeighbors[obstacle1[2].ToPosition()]);
        Assert.Contains(obstacle2[2].ToPosition(), positionToNeighbors[obstacle1[2].ToPosition()]);

        Assert.AreEqual(1, positionToNeighbors[obstacle1[3].ToPosition()].Count);
        Assert.Contains(obstacle1[2].ToPosition(), positionToNeighbors[obstacle1[3].ToPosition()]);

        // Neighbors of obstacle2
        Assert.AreEqual(2, positionToNeighbors[obstacle2[0].ToPosition()].Count);
        Assert.Contains(obstacle1[0].ToPosition(), positionToNeighbors[obstacle2[0].ToPosition()]);
        Assert.Contains(obstacle2[1].ToPosition(), positionToNeighbors[obstacle2[0].ToPosition()]);

        Assert.AreEqual(3, positionToNeighbors[obstacle2[1].ToPosition()].Count);
        Assert.Contains(obstacle1[3].ToPosition(), positionToNeighbors[obstacle2[1].ToPosition()]);
        Assert.Contains(obstacle2[2].ToPosition(), positionToNeighbors[obstacle2[1].ToPosition()]);
        Assert.Contains(obstacle2[0].ToPosition(), positionToNeighbors[obstacle2[1].ToPosition()]);

        Assert.AreEqual(1, positionToNeighbors[obstacle2[2].ToPosition()].Count);
        Assert.Contains(obstacle2[1].ToPosition(), positionToNeighbors[obstacle2[2].ToPosition()]);
    }
}