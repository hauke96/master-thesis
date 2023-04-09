using System.Collections.Generic;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common.Collections;
using MongoDB.Driver;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Tests.Graph;

public class VisibilityGraphGeneratorTest
{
    class WithLineObstacle : VisibilityGraphGeneratorTest
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
        public void GetVisibilityNeighborsForVertex()
        {
            var vertex = new Vertex(1, 0);

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
            var vertex = new Vertex(0.5, 0);

            var visibilityNeighbors = VisibilityGraphGenerator.GetVisibilityNeighborsForVertex(obstacleIndex, vertices,
                coordinateToObstacles, vertex, 2, 1);

            Assert.AreEqual(1, visibilityNeighbors.Count);
            Assert.AreEqual(2, visibilityNeighbors[0].Count);
            CollectionAssert.Contains(visibilityNeighbors[0], obstacle.Vertices[0]);
            CollectionAssert.Contains(visibilityNeighbors[0], obstacle.Vertices[1]);
        }
    }
}