using System.Collections.Generic;
using Mars.Common;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Tests;

public class WavefrontTestHelper
{
    public class WithWavefrontAlgorithm
    {
        protected static WavefrontAlgorithm wavefrontAlgorithm;
        protected static Vertex rootVertex;
        protected static LineString multiVertexLineObstacle;
        protected static LineString simpleLineObstacle;
        protected static List<Vertex> vertices;
        protected static List<Vertex> multiVertexLineVertices;
        protected static List<Vertex> simpleLineVertices;
        protected static List<Obstacle> obstacles;

        [SetUp]
        public void Setup()
        {
            Log.Init();

            multiVertexLineObstacle = new LineString(new[]
            {
                new Coordinate(6, 3),
                new Coordinate(7, 3),
                new Coordinate(7, 4),
            });
            simpleLineObstacle = new LineString(new[]
            {
                new Coordinate(2, 5),
                new Coordinate(2, 10)
            });

            multiVertexLineVertices = new List<Vertex>();
            multiVertexLineVertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[0].ToPosition(),
                multiVertexLineObstacle.Coordinates[1].ToPosition()));
            multiVertexLineVertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[1].ToPosition(),
                multiVertexLineObstacle.Coordinates[0].ToPosition(),
                multiVertexLineObstacle.Coordinates[2].ToPosition()));
            multiVertexLineVertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[2].ToPosition(),
                multiVertexLineObstacle.Coordinates[1].ToPosition()));

            simpleLineVertices = new List<Vertex>();
            simpleLineVertices.Add(new Vertex(simpleLineObstacle.Coordinates[0].ToPosition(),
                simpleLineObstacle.Coordinates[1].ToPosition()));
            simpleLineVertices.Add(new Vertex(simpleLineObstacle.Coordinates[1].ToPosition(),
                simpleLineObstacle.Coordinates[0].ToPosition()));

            vertices = new List<Vertex>();
            vertices.AddRange(multiVertexLineVertices);
            vertices.AddRange(simpleLineVertices);

            var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
            obstacleGeometries.Add(multiVertexLineObstacle);
            obstacleGeometries.Add(simpleLineObstacle);

            obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

            wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            rootVertex = new Vertex(Position.CreateGeoPosition(5, 2));
        }
    }
}