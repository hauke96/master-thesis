using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Core.Collections;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Tests;

public class VisibilityGraphGeneratorTest
{
    public class WithMultipleObstacles
    {
        private QuadTree<Obstacle> obstacleQuadTree;
        private LineString multiVertexLineObstacle;
        private LineString rotatedLineObstacle;
        private Dictionary<Coordinate, List<Coordinate>> positionToNeighbors;

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

            positionToNeighbors =
                VisibilityGraphGenerator
                    .GetObstacleNeighborsFromObstacleVertices(obstacles, new Dictionary<Coordinate, List<Obstacle>>())
                    .ToDictionary(pair =>
                        new KeyValuePair<Coordinate, List<Coordinate>>(pair.Key,
                            pair.Value.Map(v => v.ToCoordinate())));

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
            var expected = new List<Coordinate> { obstacle[1] };
            var actual = positionToNeighbors[obstacle[0]];
            Assert.AreEqual(expected, actual);

            expected = new List<Coordinate> { obstacle[0], obstacle[2] };
            actual = positionToNeighbors[obstacle[1]];
            CollectionAssert.AreEquivalent(expected, actual);

            expected = new List<Coordinate> { obstacle[1] };
            actual = positionToNeighbors[obstacle[2]];
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
            vertices = obstacle.Coordinates.Map(c => new Vertex(c));

            obstacleQuadTree = new QuadTree<Obstacle>();
            obstacleQuadTree.Insert(obstacle.Envelope, obstacle);
        }

        [Test]
        public void CalculateVisibleKnn()
        {
            var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 100);
            vertices = visibleKnn.Keys.OrderBy(v => v.ToString()).ToList();

            var bin = visibleKnn[vertices[0]];
            Assert.AreEqual(1, bin.Count);
            Assert.AreEqual(2, bin[0].Count);
            Assert.Contains(vertices[1], bin[0]);
            Assert.Contains(vertices[2], bin[0]);

            bin = visibleKnn[vertices[1]];
            Assert.AreEqual(2, bin.Count);
            Assert.AreEqual(2, bin[0].Distinct().Count());
            Assert.Contains(vertices[0], bin[0]);
            Assert.Contains(vertices[2], bin[0]);
            Assert.AreEqual(2, bin[1].Distinct().Count());
            Assert.Contains(vertices[0], bin[1]);
            Assert.Contains(vertices[2], bin[1]);

            bin = visibleKnn[vertices[2]];
            Assert.AreEqual(1, bin.Count);
            Assert.AreEqual(2, bin[0].Distinct().Count());
            Assert.Contains(vertices[0], bin[0]);
            Assert.Contains(vertices[1], bin[0]);
        }
    }

    public class CalculateVisibleKnn
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
                new Coordinate(2, 2),
                new Coordinate(2, 0.5)
            }));
            obstacleGeometries.Add(new LineString(new[]
            {
                new Coordinate(1, 1),
                new Coordinate(1, 0.9),
                new Coordinate(1, 0.8),
                new Coordinate(1, 0.7),
                new Coordinate(1, 0.6),
                new Coordinate(1, 0.5),
                new Coordinate(1, 0.4),
                new Coordinate(1, 0.3),
                new Coordinate(1, 0.2),
                new Coordinate(1, 0.1),
                new Coordinate(1, 0),
                new Coordinate(1, -0.1),
            }));

            obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));
            vertices = obstacles.SelectMany(o => o.Coordinates)
                .Map(c => new Vertex(c))
                .Distinct()
                .ToList();
            ;

            obstacleQuadTree = new QuadTree<Obstacle>();
            obstacles.Each(o => obstacleQuadTree.Insert(o.Envelope, o));
        }

        [Test]
        public void CalculateVisibleKnn_onlyNearest()
        {
            var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 1);

            // vertices[0] = vertex at (2, 2)
            var actualCoordinates = visibleKnn[vertices[0]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
            var expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[1],
                obstacles[1].Coordinates[0],
                obstacles[1].Coordinates[1],
                obstacles[1].Coordinates[2],
                obstacles[1].Coordinates[3],
                obstacles[1].Coordinates[4],
                obstacles[1].Coordinates[5],
                obstacles[1].Coordinates[6],
                obstacles[1].Coordinates[7],
                obstacles[1].Coordinates[8]
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);
        }

        [Test]
        public void CalculateVisibleKnn_someWithSameDistance()
        {
            var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 1);

            // vertices[0] = vertex at (2, 0.5)
            var actualCoordinates = visibleKnn[vertices[1]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
            var expectedCoordinates = new List<Coordinate>
            {
                obstacles[1].Coordinates[0],
                obstacles[1].Coordinates[1],
                obstacles[1].Coordinates[2],
                obstacles[1].Coordinates[3],
                obstacles[1].Coordinates[4],
                obstacles[1].Coordinates[5],
                obstacles[1].Coordinates[6],
                obstacles[1].Coordinates[7],
                obstacles[1].Coordinates[8],
                obstacles[1].Coordinates[9],
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);
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
                new Coordinate(1, 2),
                new Coordinate(1, 1)
            })); // -> left triangle (forming a square with the other triangle)
            obstacleGeometries.Add(new LineString(new[]
            {
                new Coordinate(1, 2),
                new Coordinate(2, 1),
                new Coordinate(2, 2),
                new Coordinate(1, 2)
            })); // -> right triangle (forming a square with the other triangle)
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
                .Map(c => new Vertex(c))
                .Distinct()
                .ToList();

            obstacleQuadTree = new QuadTree<Obstacle>();
            obstacles.Each(o => obstacleQuadTree.Insert(o.Envelope, o));
        }

        [Test]
        public void CalculateVisibleKnn()
        {
            var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 100);

            // vertices[0] = lower left of square (=lower left of left triangle) 
            var actualCoordinates = visibleKnn[vertices[0]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
            var expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[1],
                obstacles[0].Coordinates[2]
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

            // vertices[1] = lower right of square (=lower right of either triangle)
            actualCoordinates = visibleKnn[vertices[1]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
            expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[0],
                obstacles[1].Coordinates[2],
                obstacles[2].Coordinates[0],
                obstacles[2].Coordinates[1],
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

            // vertices[3] = upper right of square (=upper right of right triangle)
            actualCoordinates = visibleKnn[vertices[3]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
            expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[1],
                obstacles[0].Coordinates[2],
                obstacles[2].Coordinates[0],
                obstacles[2].Coordinates[1],
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

            // vertices[2] = upper left of square (=upper left of either triangle)
            actualCoordinates = visibleKnn[vertices[2]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
            expectedCoordinates = new List<Coordinate>
            {
                obstacles[0].Coordinates[0],
                obstacles[1].Coordinates[2],
                obstacles[2].Coordinates[1],
            };
            CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);
        }
    }

    class GetVisibilityNeighborsForVertex : VisibilityGraphGeneratorTest
    {
        Obstacle obstacle;
        QuadTree<Obstacle> obstacleIndex;
        List<Vertex> vertices;
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles;

        [SetUp]
        public void Setup()
        {
            obstacle = new Obstacle(new LineString(new[]
            {
                new Coordinate(0, 1),
                new Coordinate(1, 1),
                new Coordinate(2, 1)
            }));

            obstacleIndex = new QuadTree<Obstacle>();
            obstacleIndex.Insert(obstacle.Envelope, obstacle);

            vertices = obstacle.Vertices;

            coordinateToObstacles = new Dictionary<Coordinate, List<Obstacle>>();
            coordinateToObstacles.Add(obstacle.Coordinates[0], new List<Obstacle> { obstacle });
            coordinateToObstacles.Add(obstacle.Coordinates[1], new List<Obstacle> { obstacle });
            coordinateToObstacles.Add(obstacle.Coordinates[2], new List<Obstacle> { obstacle });
        }

        [Test]
        public void GetVisibilityNeighborsForVertex_correctlyDeterminesNeighbors()
        {
            var vertex = new Vertex(new Coordinate(1, 0));

            var visibilityNeighbors = VisibilityGraphGenerator.GetVisibilityNeighborsForVertex(obstacleIndex, vertices,
                coordinateToObstacles, vertex);

            Assert.AreEqual(1, visibilityNeighbors.Count);
            Assert.AreEqual(3, visibilityNeighbors[0].Count);
            CollectionAssert.AreEquivalent(vertices, visibilityNeighbors[0]);
        }

        [Test]
        public void GetVisibilityNeighborsForVertex_vertexOnObstacle()
        {
            var vertex = obstacle.Vertices[0];

            var visibilityNeighbors = VisibilityGraphGenerator.GetVisibilityNeighborsForVertex(obstacleIndex, vertices,
                coordinateToObstacles, vertex);

            Assert.AreEqual(1, visibilityNeighbors.Count);
            Assert.AreEqual(2, visibilityNeighbors[0].Count);
            CollectionAssert.Contains(visibilityNeighbors[0], obstacle.Vertices[1]);
            CollectionAssert.Contains(visibilityNeighbors[0], obstacle.Vertices[2]);
        }

        [Test]
        public void GetVisibilityNeighborsForVertex_180DegreeBinsOfSizeOne()
        {
            // Obstacle vertex 0 is one the left and the two other on the right -> only one of the right vertices should be taken
            var vertex = new Vertex(new Coordinate(0.5, 0));

            var visibilityNeighbors = VisibilityGraphGenerator.GetVisibilityNeighborsForVertex(obstacleIndex, vertices,
                coordinateToObstacles, vertex, 2, 1);

            Assert.AreEqual(1, visibilityNeighbors.Count);
            Assert.AreEqual(2, visibilityNeighbors[0].Count);
            CollectionAssert.Contains(visibilityNeighbors[0], obstacle.Vertices[0]);
            CollectionAssert.Contains(visibilityNeighbors[0], obstacle.Vertices[1]);
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
            VisibilityGraphGenerator
                .GetObstacleNeighborsFromObstacleVertices(new List<Obstacle> { new(obstacle) },
                    new Dictionary<Coordinate, List<Obstacle>>())
                .ToDictionary(pair =>
                    new KeyValuePair<Coordinate, List<Coordinate>>(pair.Key,
                        pair.Value.Map(v => v.ToCoordinate())));

        Assert.AreEqual(2, positionToNeighbors.Count);

        Assert.AreEqual(1, positionToNeighbors[obstacle[0]].Count);
        Assert.Contains(obstacle[1], positionToNeighbors[obstacle[0]]);

        Assert.AreEqual(1, positionToNeighbors[obstacle[1]].Count);
        Assert.Contains(obstacle[0], positionToNeighbors[obstacle[1]]);
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
            VisibilityGraphGenerator
                .GetObstacleNeighborsFromObstacleVertices(new List<Obstacle> { new(obstacle) },
                    new Dictionary<Coordinate, List<Obstacle>>())
                .ToDictionary(pair =>
                    new KeyValuePair<Coordinate, List<Coordinate>>(pair.Key,
                        pair.Value.Map(v => v.ToCoordinate())));

        Assert.AreEqual(3, positionToNeighbors.Count);

        Assert.AreEqual(1, positionToNeighbors[obstacle[0]].Count);
        Assert.Contains(obstacle[1], positionToNeighbors[obstacle[0]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle[1]].Count);
        Assert.Contains(obstacle[2], positionToNeighbors[obstacle[1]]);
        Assert.Contains(obstacle[0], positionToNeighbors[obstacle[1]]);

        Assert.AreEqual(1, positionToNeighbors[obstacle[2]].Count);
        Assert.Contains(obstacle[1], positionToNeighbors[obstacle[2]]);
    }

    [Test]
    public void GetNeighborsFromObstacleVertices_Polygon()
    {
        var obstacle = new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(1, 1),
            new Coordinate(0, 0)
        });

        var coordinateToObstacles =
            VisibilityGraphGenerator.GetCoordinateToObstaclesMapping(new List<Obstacle> { new(obstacle) });
        var positionToNeighbors =
            VisibilityGraphGenerator
                .GetObstacleNeighborsFromObstacleVertices(new List<Obstacle> { new(obstacle) }, coordinateToObstacles)
                .ToDictionary(pair =>
                    new KeyValuePair<Coordinate, List<Coordinate>>(pair.Key,
                        pair.Value.Map(v => v.ToCoordinate())));

        Assert.AreEqual(3, positionToNeighbors.Count);

        Assert.AreEqual(2, positionToNeighbors[obstacle[0]].Count);
        Assert.Contains(obstacle[1], positionToNeighbors[obstacle[0]]);
        Assert.Contains(obstacle[2], positionToNeighbors[obstacle[0]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle[1]].Count);
        Assert.Contains(obstacle[0], positionToNeighbors[obstacle[1]]);
        Assert.Contains(obstacle[2], positionToNeighbors[obstacle[1]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle[2]].Count);
        Assert.Contains(obstacle[0], positionToNeighbors[obstacle[2]]);
        Assert.Contains(obstacle[1], positionToNeighbors[obstacle[2]]);
    }

    [Test]
    public void GetNeighborsFromObstacleVertices_TouchingPolygon()
    {
        var obstacle1 = new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(1, 1),
            new Coordinate(0, 0)
        });
        var obstacle2 = new LineString(new[]
        {
            new Coordinate(1, 0),
            new Coordinate(2, 0),
            new Coordinate(1, 1),
            new Coordinate(1, 0)
        });

        var list = new List<Obstacle> { new(obstacle1), new(obstacle2) };

        var coordinateToObstacles =
            VisibilityGraphGenerator.GetCoordinateToObstaclesMapping(new List<Obstacle>
                { new(obstacle1), new(obstacle2) });
        var positionToNeighbors =
            VisibilityGraphGenerator
                .GetObstacleNeighborsFromObstacleVertices(list, coordinateToObstacles)
                .ToDictionary(pair =>
                    new KeyValuePair<Coordinate, List<Coordinate>>(pair.Key,
                        pair.Value.Map(v => v.ToCoordinate())));

        Assert.AreEqual(4, positionToNeighbors.Count);

        // Neighbors of obstacle1
        Assert.AreEqual(2, positionToNeighbors[obstacle1[0]].Count);
        Assert.Contains(obstacle1[1], positionToNeighbors[obstacle1[0]]);
        Assert.Contains(obstacle1[2], positionToNeighbors[obstacle1[0]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[1]].Count);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle1[1]]);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle1[1]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[2]].Count);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle1[2]]);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle1[2]]);

        // Neighbors of obstacle2
        Assert.AreEqual(2, positionToNeighbors[obstacle2[0]].Count);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle2[0]]);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle2[0]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle2[1]].Count);
        Assert.Contains(obstacle2[0], positionToNeighbors[obstacle2[1]]);
        Assert.Contains(obstacle2[2], positionToNeighbors[obstacle2[1]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle2[2]].Count);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle2[2]]);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle2[2]]);
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

        var positionToNeighbors =
            VisibilityGraphGenerator
                .GetObstacleNeighborsFromObstacleVertices(list, new Dictionary<Coordinate, List<Obstacle>>())
                .ToDictionary(pair =>
                    new KeyValuePair<Coordinate, List<Coordinate>>(pair.Key,
                        pair.Value.Map(v => v.ToCoordinate())));

        Assert.AreEqual(5, positionToNeighbors.Count);

        // Neighbors of obstacle1
        Assert.AreEqual(1, positionToNeighbors[obstacle1[0]].Count);
        Assert.Contains(obstacle1[1], positionToNeighbors[obstacle1[0]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[1]].Count);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle1[1]]);
        Assert.Contains(obstacle1[2], positionToNeighbors[obstacle1[1]]);

        Assert.AreEqual(3, positionToNeighbors[obstacle1[2]].Count);
        Assert.Contains(obstacle1[1], positionToNeighbors[obstacle1[2]]);
        Assert.Contains(obstacle1[3], positionToNeighbors[obstacle1[2]]);
        Assert.Contains(obstacle2[2], positionToNeighbors[obstacle1[2]]);

        Assert.AreEqual(1, positionToNeighbors[obstacle1[3]].Count);
        Assert.Contains(obstacle1[2], positionToNeighbors[obstacle1[3]]);

        // Neighbors of obstacle2
        Assert.AreEqual(2, positionToNeighbors[obstacle2[0]].Count);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle2[0]]);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle2[0]]);

        Assert.AreEqual(3, positionToNeighbors[obstacle2[1]].Count);
        Assert.Contains(obstacle1[3], positionToNeighbors[obstacle2[1]]);
        Assert.Contains(obstacle2[2], positionToNeighbors[obstacle2[1]]);
        Assert.Contains(obstacle2[0], positionToNeighbors[obstacle2[1]]);

        Assert.AreEqual(1, positionToNeighbors[obstacle2[2]].Count);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle2[2]]);
    }

    [Test]
    public void SortVisibleNeighborsIntoBins()
    {
        var neighbors = new List<Position>
        {
            new(1.1, 2), // Little >0°
            new(2.0, 1), // 90°
            new(0.0, 1), // 270°
        };
        var neighborVertices = new List<Position>
        {
            new(1, 2), // 0°
            new(2, 2), // 45°
            new(3, 1), // 90°
            new(1, 0), // 180°
            new(0, 2), // 315°
        }.Map(n => new Vertex(n.ToCoordinate()));

        var vertex = new Vertex(new Coordinate(1, 1), neighbors);

        var allNeighbors = neighborVertices.CreateCopy();
        allNeighbors.AddRange(neighbors.Map(n => new Vertex(n.ToCoordinate())));
        var bins = VisibilityGraphGenerator.SortVisibilityNeighborsIntoBins(vertex, allNeighbors);

        Assert.Contains(neighborVertices[0], bins[2]);
        Assert.Contains(neighborVertices[1], bins[0]);
        Assert.Contains(neighborVertices[2], bins[0]);
        Assert.Contains(neighborVertices[2], bins[1]);
        Assert.Contains(neighborVertices[3], bins[1]);
        Assert.Contains(neighborVertices[4], bins[2]);
    }
}