using System.Collections.Generic;
using Mars.Common;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Tests;

public class WavefrontTestHelper
{
    public class WithWavefrontAlgorithm
    {
        protected static LineString multiVertexLineObstacle;
        protected static LineString simpleLineObstacle;
        protected static List<Vertex> vertices;
        protected static List<Vertex> multiVertexLineVertices;
        protected static List<Vertex> simpleLineVertices;

        [SetUp]
        public void Setup()
        {
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
        }
    }
}