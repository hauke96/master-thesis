using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common;
using Mars.Common.Collections;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Tests;

public class VisibilityGraphGeneratorTest
{
    [Test]
    public void CalculateVisibleKnn_onSimpleLineString()
    {
        var obstacle = ObstacleTestHelper.CreateObstacle(new LineString(new[]
        {
            new Coordinate(1, 1),
            new Coordinate(1, 2),
            new Coordinate(2, 3)
        }));

        var obstacleQuadTree = new QuadTree<Obstacle>();
        obstacleQuadTree.Insert(obstacle.Envelope, obstacle);

        var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 100);
        var vertices = visibleKnn.Keys.OrderBy(v => v.ToString()).ToList();

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

    [Test]
    public void CalculateVisibleKnn_onSimplePolygon()
    {
        var obstacle = ObstacleTestHelper.CreateObstacle(new Polygon(new LinearRing(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(1, 1),
            new Coordinate(0, 0)
        })));

        var obstacleQuadTree = new QuadTree<Obstacle>();
        obstacleQuadTree.Insert(obstacle.Envelope, obstacle);

        var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 100);
        var vertices = obstacle.Vertices;

        List<List<Vertex>> bin;
        bin = visibleKnn[vertices[0]];
        Assert.AreEqual(2, bin.Count);
        Assert.AreEqual(2, bin[0].Count);
        Assert.Contains(vertices[1], bin[0]);
        Assert.Contains(vertices[2], bin[0]);
        Assert.AreEqual(2, bin[1].Count);
        Assert.Contains(vertices[1], bin[1]);
        Assert.Contains(vertices[2], bin[1]);

        bin = visibleKnn[vertices[1]];
        Assert.AreEqual(2, bin.Count);
        Assert.AreEqual(2, bin[0].Count);
        Assert.Contains(vertices[0], bin[0]);
        Assert.Contains(vertices[2], bin[0]);
        Assert.AreEqual(2, bin[1].Count);
        Assert.Contains(vertices[0], bin[1]);
        Assert.Contains(vertices[2], bin[1]);

        bin = visibleKnn[vertices[2]];
        Assert.AreEqual(2, bin.Count);
        Assert.AreEqual(2, bin[0].Count);
        Assert.Contains(vertices[0], bin[0]);
        Assert.Contains(vertices[1], bin[0]);
        Assert.AreEqual(2, bin[1].Count);
        Assert.Contains(vertices[0], bin[1]);
        Assert.Contains(vertices[1], bin[1]);
    }

    [Test]
    public void CalculateVisibleKnn_onTouchingPolygons()
    {
        var obstacles = ObstacleTestHelper.CreateObstacles(new Polygon(new LinearRing(new[]
            {
                new Coordinate(0, 0),
                new Coordinate(1, 0),
                new Coordinate(1, 1),
                new Coordinate(0, 0)
            })),
            new Polygon(new LinearRing(new[]
            {
                new Coordinate(1, 0),
                new Coordinate(2, 0),
                new Coordinate(1, 1),
                new Coordinate(1, 0)
            }))
        );

        var obstacle1 = obstacles[0];
        var obstacle2 = obstacles[1];

        var obstacleQuadTree = new QuadTree<Obstacle>();
        obstacleQuadTree.Insert(obstacle1.Envelope, obstacle1);
        obstacleQuadTree.Insert(obstacle2.Envelope, obstacle2);

        var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 100);
        var vertices1 = obstacle1.Vertices;
        var vertices2 = obstacle2.Vertices;

        List<List<Vertex>> bin;

        // Bottom left
        bin = visibleKnn[vertices1[0]];
        Assert.AreEqual(2, bin.Count);
        Assert.AreEqual(2, bin[0].Count);
        Assert.Contains(vertices1[1], bin[0]);
        Assert.Contains(vertices1[2], bin[0]);
        Assert.AreEqual(2, bin[1].Count);
        Assert.Contains(vertices1[1], bin[1]);
        Assert.Contains(vertices1[2], bin[1]);

        // Bottom middle
        bin = visibleKnn[vertices1[1]];
        Assert.AreEqual(2, bin.Count);
        Assert.AreEqual(2, bin[0].Count);
        Assert.Contains(vertices1[0], bin[0]);
        Assert.Contains(vertices2[1], bin[0]);
        Assert.AreEqual(2, bin[1].Count);
        Assert.Contains(vertices1[0], bin[1]);
        Assert.Contains(vertices2[1], bin[1]);

        // Bottom right
        bin = visibleKnn[vertices2[1]];
        Assert.AreEqual(2, bin.Count);
        Assert.AreEqual(2, bin[0].Count);
        Assert.Contains(vertices2[0], bin[0]);
        Assert.Contains(vertices2[2], bin[0]);
        Assert.AreEqual(2, bin[1].Count);
        Assert.Contains(vertices2[0], bin[1]);
        Assert.Contains(vertices2[2], bin[1]);

        // Bottom left
        bin = visibleKnn[vertices1[0]];
        Assert.AreEqual(2, bin.Count);
        Assert.AreEqual(2, bin[0].Count);
        Assert.Contains(vertices1[1], bin[0]);
        Assert.Contains(vertices1[2], bin[0]);
        Assert.AreEqual(2, bin[1].Count);
        Assert.Contains(vertices1[1], bin[1]);
        Assert.Contains(vertices1[2], bin[1]);
    }

    [Test]
    public void CalculateVisibleKnn_onlyNearestKPerBin()
    {
        // Arrange
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

        var obstacles = ObstacleTestHelper.CreateObstacles(obstacleGeometries.ToArray());
        var vertices = obstacles.SelectMany(o => o.Coordinates)
            .Map(c => new Vertex(c))
            .Distinct()
            .ToList();

        var obstacleQuadTree = new QuadTree<Obstacle>();
        obstacles.Each(o => obstacleQuadTree.Insert(o.Envelope, o));

        // Act
        var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 1, 10);

        // Assert
        IEnumerable<Coordinate> actualCoordinates;
        List<Coordinate> expectedCoordinates;

        // vertices[0] = vertex at (2, 2)
        actualCoordinates = visibleKnn[vertices[0]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
        expectedCoordinates = new List<Coordinate>
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

        // vertices[1] = vertex at (2, 0.5)
        actualCoordinates = visibleKnn[vertices[1]].SelectMany(x => x).Map(v => v.Coordinate).Distinct();
        expectedCoordinates = new List<Coordinate>
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

    [Test]
    public void CalculateVisibleKnn_collinearObstacles()
    {
        // Arrange
        var obstacles = ObstacleTestHelper.CreateObstacles(
            new LineString(new[] // -> left triangle (forming a square with the other triangle)
            {
                new Coordinate(1, 1),
                new Coordinate(2, 1),
                new Coordinate(1, 2),
                new Coordinate(1, 1)
            }), new LineString(new[] // -> right triangle (forming a square with the other triangle)
            {
                new Coordinate(1, 2),
                new Coordinate(2, 1),
                new Coordinate(2, 2),
                new Coordinate(1, 2)
            }), new LineString(new[] // -> "r"
            {
                new Coordinate(3, 1),
                new Coordinate(3, 2),
                new Coordinate(4, 2)
            }), new LineString(new[] // -> "u"
            {
                new Coordinate(5, 2),
                new Coordinate(5, 1),
                new Coordinate(6, 1),
                new Coordinate(6, 2),
            })
        );
        var vertices = obstacles.SelectMany(o => o.Coordinates)
            .Map(c => new Vertex(c))
            .Distinct()
            .ToList();

        var obstacleQuadTree = new QuadTree<Obstacle>();
        obstacles.Each(o => obstacleQuadTree.Insert(o.Envelope, o));

        // Act
        var visibleKnn = VisibilityGraphGenerator.CalculateVisibleKnn(obstacleQuadTree, 100);

        // Assert
        // lower left of square (=lower left of left triangle) 
        var actualCoordinates = visibleKnn[ObstacleTestHelper.VertexAt(vertices, obstacles[0].Coordinates[0])]
            .SelectMany(x => x)
            .Map(v => v.Coordinate).Distinct();
        var expectedCoordinates = new List<Coordinate>
        {
            obstacles[0].Coordinates[1],
            obstacles[0].Coordinates[2]
        };
        CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

        // lower right of square (=lower right of either triangle)
        actualCoordinates = visibleKnn[ObstacleTestHelper.VertexAt(vertices, obstacles[0].Coordinates[1])]
            .SelectMany(x => x)
            .Map(v => v.Coordinate).Distinct();
        expectedCoordinates = new List<Coordinate>
        {
            obstacles[0].Coordinates[0],
            obstacles[1].Coordinates[2],
            obstacles[2].Coordinates[0],
            obstacles[2].Coordinates[1],
        };
        CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

        // upper right of square (=upper right of right triangle)
        actualCoordinates = visibleKnn[ObstacleTestHelper.VertexAt(vertices, obstacles[1].Coordinates[2])]
            .SelectMany(x => x)
            .Map(v => v.Coordinate).Distinct();
        expectedCoordinates = new List<Coordinate>
        {
            obstacles[0].Coordinates[1],
            obstacles[0].Coordinates[2],
            obstacles[2].Coordinates[0],
            obstacles[2].Coordinates[1],
        };
        CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);

        // upper left of square (=upper left of either triangle)
        actualCoordinates = visibleKnn[ObstacleTestHelper.VertexAt(vertices, obstacles[1].Coordinates[0])]
            .SelectMany(x => x)
            .Map(v => v.Coordinate).Distinct();
        expectedCoordinates = new List<Coordinate>
        {
            obstacles[0].Coordinates[0],
            obstacles[1].Coordinates[2],
            obstacles[2].Coordinates[1],
        };
        CollectionAssert.AreEquivalent(expectedCoordinates, actualCoordinates);
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
            obstacle = ObstacleTestHelper.CreateObstacle(new LineString(new[]
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
    public void AddObstacleNeighborsForObstacles_MultiVertexLineString()
    {
        var obstacle = new LineString(new[]
        {
            new Coordinate(6, 3),
            new Coordinate(7, 3),
            new Coordinate(7, 4),
        });
        var obstacles = ObstacleTestHelper.CreateObstacles(obstacle);

        VisibilityGraphGenerator.AddObstacleNeighborsForObstacles(obstacles,
            new Dictionary<Coordinate, List<Obstacle>>());

        var positionToNeighbors = GetPositionToNeighborMap(obstacles);

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
    public void AddObstacleNeighborsForObstacles_Polygon()
    {
        var obstacle = new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(1, 1),
            new Coordinate(0, 0)
        });
        var obstacles = ObstacleTestHelper.CreateObstacles(obstacle);
        var coordinateToObstacles = VisibilityGraphGenerator.GetCoordinateToObstaclesMapping(obstacles);

        VisibilityGraphGenerator.AddObstacleNeighborsForObstacles(obstacles, coordinateToObstacles);

        var positionToNeighbors = GetPositionToNeighborMap(obstacles);

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
    public void AddObstacleNeighborsForObstacles_TouchingPolygon()
    {
        // Left and right triangles touching in the middle:
        //    /|\
        //   / | \
        //  /__|__\
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

        var obstacles = ObstacleTestHelper.CreateObstacles(obstacle1, obstacle2);
        var coordinateToObstacles = VisibilityGraphGenerator.GetCoordinateToObstaclesMapping(obstacles);

        VisibilityGraphGenerator.AddObstacleNeighborsForObstacles(obstacles, coordinateToObstacles);

        var positionToNeighbors = GetPositionToNeighborMap(obstacles);

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
    public void AddObstacleNeighborsForObstacles_OverlappingLines()
    {
        var obstacle1 = new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 1), // Intersection with obstacle 2
            new Coordinate(0, 2), // Intersection with obstacle 2
            new Coordinate(1, 3)
        });
        var obstacle2 = new LineString(new[]
        {
            new Coordinate(0, 1), // Intersection with obstacle 1
            new Coordinate(0, 2), // Intersection with obstacle 1
            new Coordinate(0, 3)
        });
        var obstacles = ObstacleTestHelper.CreateObstacles(obstacle1, obstacle2);

        VisibilityGraphGenerator.AddObstacleNeighborsForObstacles(obstacles,
            new Dictionary<Coordinate, List<Obstacle>>());

        var positionToNeighbors = GetPositionToNeighborMap(obstacles);

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
    public void AddObstacleNeighborsForObstacles_TouchingLines()
    {
        var obstacle1 = new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 1)
        });
        var obstacle2 = new LineString(new[]
        {
            new Coordinate(0, 1),
            new Coordinate(0, 2)
        });
        var obstacles = ObstacleTestHelper.CreateObstacles(obstacle1, obstacle2);

        VisibilityGraphGenerator.AddObstacleNeighborsForObstacles(obstacles,
            new Dictionary<Coordinate, List<Obstacle>>());

        var positionToNeighbors = GetPositionToNeighborMap(obstacles);

        Assert.AreEqual(3, positionToNeighbors.Count);

        // Neighbors of obstacle1
        Assert.AreEqual(1, positionToNeighbors[obstacle1[0]].Count);
        Assert.Contains(obstacle1[1], positionToNeighbors[obstacle1[0]]);

        Assert.AreEqual(2, positionToNeighbors[obstacle1[1]].Count);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle1[1]]);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle1[1]]);

        // Neighbors of obstacle2
        Assert.AreEqual(2, positionToNeighbors[obstacle2[0]].Count);
        Assert.Contains(obstacle1[0], positionToNeighbors[obstacle2[0]]);
        Assert.Contains(obstacle2[1], positionToNeighbors[obstacle2[0]]);

        Assert.AreEqual(1, positionToNeighbors[obstacle2[1]].Count);
        Assert.Contains(obstacle2[0], positionToNeighbors[obstacle2[1]]);
    }

    [Test]
    public void SortVisibleNeighborsIntoBins()
    {
        var obstacleNeighbors = new List<Position>
        {
            new(1.1, 2), // Little >0°
            new(2.0, 1), // 90°
            new(0.0, 1), // 270°
        };
        var visibilityNeighbors = new List<Position>
        {
            new(1, 2), // 0°
            new(2, 2), // 45°
            new(3, 1), // 90°
            new(1, 0), // 180°
            new(0, 2), // 315°
        }.Map(n => new Vertex(n.ToCoordinate()));

        var vertex = new Vertex(new Coordinate(1, 1), obstacleNeighbors);

        var allVisibilityNeighbors = visibilityNeighbors.CreateCopy();
        allVisibilityNeighbors.AddRange(obstacleNeighbors.Map(n => new Vertex(n.ToCoordinate())));
        var bins = VisibilityGraphGenerator.SortVisibilityNeighborsIntoBins(vertex, allVisibilityNeighbors);

        Assert.Contains(visibilityNeighbors[0], bins[2]);
        Assert.Contains(visibilityNeighbors[1], bins[0]);
        Assert.Contains(visibilityNeighbors[2], bins[0]);
        Assert.Contains(visibilityNeighbors[2], bins[1]);
        Assert.Contains(visibilityNeighbors[3], bins[1]);
        Assert.Contains(visibilityNeighbors[4], bins[2]);
    }

    private static Dictionary<Coordinate, List<Coordinate>> GetPositionToNeighborMap(List<Obstacle> obstacles)
    {
        return obstacles.Map(o => o.Vertices)
            .SelectMany(x => x)
            .Distinct()
            .ToDictionary(
                v => v.Coordinate,
                v => v.ObstacleNeighbors.Map(n => n.ToCoordinate())
            );
    }
}