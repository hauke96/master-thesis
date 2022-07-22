using System;
using System.Collections.Generic;
using Mars.Common;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Tests
{
    public class WavefrontAlgorithmTest
    {
        public class WithWavefrontAlgorithm
        {
            static WavefrontAlgorithm wavefrontAlgorithm;
            static Vertex rootVertex;
            static List<Wavefront> wavefronts;
            static LineString multiVertexLineObstacle;
            static LineString simpleLineObstacle;
            static List<Vertex> vertices;
            static List<Vertex> multiVertexLineVertices;
            static List<Vertex> simpleLineVertices;

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

                var obstacles = new List<NetTopologySuite.Geometries.Geometry>();
                obstacles.Add(multiVertexLineObstacle);
                obstacles.Add(simpleLineObstacle);

                wavefronts = new List<Wavefront>();
                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles, wavefronts);
                rootVertex = new Vertex(Position.CreateGeoPosition(5, 2));
            }

            public class NeighborsFromObstacleVertices : WithWavefrontAlgorithm
            {
                [SetUp]
                public void setup()
                {
                }

                [Test]
                public void GetNeighborsFromObstacleVertices_SimpleLineString()
                {
                    var positionToNeighbors =
                        wavefrontAlgorithm.GetNeighborsFromObstacleVertices(
                            new List<NetTopologySuite.Geometries.Geometry> { simpleLineObstacle });

                    Assert.AreEqual(2, positionToNeighbors.Count);

                    Assert.AreEqual(1, positionToNeighbors[simpleLineObstacle[0].ToPosition()].Count);
                    Assert.AreEqual(simpleLineObstacle[1].ToPosition(),
                        positionToNeighbors[simpleLineObstacle[0].ToPosition()][0]);

                    Assert.AreEqual(1, positionToNeighbors[simpleLineObstacle[1].ToPosition()].Count);
                    Assert.AreEqual(simpleLineObstacle[0].ToPosition(),
                        positionToNeighbors[simpleLineObstacle[1].ToPosition()][0]);
                }

                [Test]
                public void GetNeighborsFromObstacleVertices_MultiVertexLineString()
                {
                    var positionToNeighbors =
                        wavefrontAlgorithm.GetNeighborsFromObstacleVertices(
                            new List<NetTopologySuite.Geometries.Geometry> { multiVertexLineObstacle });

                    Assert.AreEqual(3, positionToNeighbors.Count);

                    Assert.AreEqual(1, positionToNeighbors[multiVertexLineObstacle[0].ToPosition()].Count);
                    Assert.AreEqual(multiVertexLineObstacle[1].ToPosition(),
                        positionToNeighbors[multiVertexLineObstacle[0].ToPosition()][0]);

                    Assert.AreEqual(2, positionToNeighbors[multiVertexLineObstacle[1].ToPosition()].Count);
                    Assert.AreEqual(multiVertexLineObstacle[2].ToPosition(),
                        positionToNeighbors[multiVertexLineObstacle[1].ToPosition()][0]);
                    Assert.AreEqual(multiVertexLineObstacle[0].ToPosition(),
                        positionToNeighbors[multiVertexLineObstacle[1].ToPosition()][1]);

                    Assert.AreEqual(1, positionToNeighbors[multiVertexLineObstacle[2].ToPosition()].Count);
                    Assert.AreEqual(multiVertexLineObstacle[1].ToPosition(),
                        positionToNeighbors[multiVertexLineObstacle[2].ToPosition()][0]);
                }
            }

            public class ProcessNextEvent : WithWavefrontAlgorithm
            {
                Position targetPosition;

                [SetUp]
                public void setup()
                {
                    targetPosition = Position.CreateGeoPosition(10, 10);
                }

                [Test]
                public void WavefrontHasNoNextVertex()
                {
                    var vertices = new List<Vertex>();
                    vertices.Add(new Vertex(6.5, 3.1));
                    var wavefront = Wavefront.New(0, 90, new Vertex(1, 1), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    // Remove last remaining vertex
                    wavefront.RemoveNextVertex();

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(0, wavefrontAlgorithm.Wavefronts.Count);
                }

                [Test]
                public void EventIsNotValid()
                {
                    var vertices = new List<Vertex>();
                    vertices.Add(new Vertex(6.5, 3.1));
                    // Between but slightly below the multi-vertex-line
                    var wavefront = Wavefront.New(0, 90, new Vertex(6.5, 2.9), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(0, wavefront.RelevantVertices.Count);
                }

                [Test]
                public void EventVertexHasAlreadyBeenProcessed()
                {
                    var vertices = new List<Vertex>();
                    var nextVertex = new Vertex(multiVertexLineObstacle.Coordinates[0].ToPosition());
                    vertices.Add(nextVertex);
                    var wavefront = Wavefront.New(0, 90, new Vertex(5, 2), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    wavefrontAlgorithm.PositionToPredecessor[nextVertex.Position] = Position.CreateGeoPosition(1, 1);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(0, wavefront.RelevantVertices.Count);
                }

                [Test]
                public void FirstVertexHasNeighbors_NotCastingShadow()
                {
                    var vertices = new List<Vertex>();
                    vertices.AddRange(wavefrontAlgorithm.Vertices);
                    vertices.Add(new Vertex(5, 2.5));
                    var wavefront = Wavefront.New(180, 270, new Vertex(7.5, 3.5), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);

                    var w = wavefrontAlgorithm.Wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefrontAlgorithm.Wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 225) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 270) < 1);
                    Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);
                }

                [Test]
                public void TargetReached()
                {
                    var vertices = new List<Vertex>();
                    var sourceVertex = multiVertexLineVertices[0];
                    var targetVertex = new Vertex(targetPosition);
                    vertices.Add(targetVertex);

                    var wavefront = Wavefront.New(0, 90, sourceVertex, vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(wavefront.RootVertex.Position,
                        wavefrontAlgorithm.PositionToPredecessor[targetPosition]);
                    Assert.AreEqual(0, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(0, wavefront.RelevantVertices.Count);
                }

                [Test]
                public void CastingShadowWithRootBeingNeighborOfShadowEdge()
                {
                    // Real world problem: The shadow ends at 180° but the wavefront ends there as well -> There was a
                    // problem adjusting the wavefront

                    var vertices = new List<Vertex>();
                    var line = new LineString(new[]
                    {
                        new Coordinate(0.05200529027, 0.04528037038),
                        new Coordinate(0.05200350079, 0.0318479355),
                        new Coordinate(0.06326695096, 0.03184643498),
                        new Coordinate(0.06326760583, 0.03676209134),
                        new Coordinate(0.06327136644, 0.06499057746),
                    });
                    vertices.Add(new Vertex(line.Coordinates[0].ToPosition(), line.Coordinates[^1].ToPosition(),
                        line.Coordinates[1].ToPosition()));
                    vertices.Add(new Vertex(line.Coordinates[1].ToPosition(), line.Coordinates[0].ToPosition(),
                        line.Coordinates[2].ToPosition()));
                    vertices.Add(new Vertex(line.Coordinates[2].ToPosition(), line.Coordinates[1].ToPosition(),
                        line.Coordinates[3].ToPosition()));
                    vertices.Add(new Vertex(line.Coordinates[3].ToPosition(), line.Coordinates[2].ToPosition(),
                        line.Coordinates[4].ToPosition()));
                    vertices.Add(new Vertex(line.Coordinates[4].ToPosition(), line.Coordinates[3].ToPosition(),
                        line.Coordinates[^1].ToPosition()));
                    vertices.Add(new Vertex(0.0820386, 0.0409633));
                    vertices.Add(new Vertex(0.0104371, 0.082151));

                    var sourceVertex = vertices[0];
                    var vertex = vertices[2];

                    var wavefront = Wavefront.New(269.992367019, 180.00763298898323, sourceVertex, vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);

                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(vertices[1].Position));
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(vertices[3].Position));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront.FromAngle, w.FromAngle, 0.1);
                    Assert.AreEqual(360, w.ToAngle, 0.1);

                    w = wavefronts[1];
                    Assert.AreEqual(0, w.FromAngle, 0.1);
                    Assert.AreEqual(127.1, w.ToAngle, 0.1);
                }
            }

            public class WithMultiVertexLineFullyInsideWavefront : WithWavefrontAlgorithm
            {
                Wavefront wavefront;
                Vertex nextVertex;
                Position targetPosition;

                [SetUp]
                public void Setup()
                {
                    nextVertex = multiVertexLineVertices[0];
                    wavefront = Wavefront.New(0, 350, new Vertex(5, 0), wavefrontAlgorithm.Vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    targetPosition = Position.CreateGeoPosition(10, 10);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);
                }

                [Test]
                public void SetsPredecessorCorrectly()
                {
                    Assert.AreEqual(wavefront.RootVertex.Position,
                        wavefrontAlgorithm.PositionToPredecessor[nextVertex.Position]);
                }

                [Test]
                public void RemovesNextVertexFromWavefront()
                {
                    Assert.IsFalse(wavefront.RelevantVertices.Contains(nextVertex));
                }

                [Test]
                public void NotCastingShadow_NotRemovingOldWavefront()
                {
                    Assert.IsTrue(wavefrontAlgorithm.Wavefronts.Contains(wavefront));
                    Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);

                    var w = wavefrontAlgorithm.Wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefrontAlgorithm.Wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 18.43) < 1);
                    Assert.AreEqual(90, w.ToAngle);
                    Assert.AreEqual(multiVertexLineObstacle[0], w.RootVertex.Coordinate);
                }

                [Test]
                public void NextVertexCastingShadow()
                {
                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.IsFalse(wavefrontAlgorithm.Wavefronts.Contains(wavefront));
                    Assert.AreEqual(3, wavefrontAlgorithm.Wavefronts.Count);

                    var w = wavefrontAlgorithm.Wavefronts[0];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 18.43) < 1);
                    Assert.AreEqual(90, w.ToAngle);
                    Assert.AreEqual(multiVertexLineObstacle[0], w.RootVertex.Coordinate);

                    w = wavefrontAlgorithm.Wavefronts[1];
                    Assert.AreEqual(0, w.FromAngle);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 33.69) < 1);
                    Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);

                    w = wavefrontAlgorithm.Wavefronts[2];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 33.69) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 350) < 1);
                    Assert.AreEqual(wavefront.RootVertex, w.RootVertex);
                }
            }

            public class NewWavefrontExceedingZeroDegree : WithWavefrontAlgorithm
            {
                Wavefront wavefront;
                Vertex nextVertex;
                Position targetPosition;

                [SetUp]
                public void setup()
                {
                    nextVertex = multiVertexLineVertices[0];
                    // Add wavefront close to the next vertex
                    wavefront = Wavefront.New(270, 355, new Vertex(6.2, 2.8), wavefrontAlgorithm.Vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    targetPosition = Position.CreateGeoPosition(10, 10);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);
                }

                [Test]
                public void SetsPredecessorCorrectly()
                {
                    Assert.AreEqual(wavefront.RootVertex.Position,
                        wavefrontAlgorithm.PositionToPredecessor[nextVertex.Position]);
                }

                [Test]
                public void RemovesNextVertexFromWavefront()
                {
                    Assert.IsFalse(wavefront.RelevantVertices.Contains(nextVertex));
                }

                [Test]
                public void ReplacesOriginalWavefront()
                {
                    Assert.Contains(wavefront, wavefrontAlgorithm.Wavefronts);
                    Assert.AreEqual(3, wavefrontAlgorithm.Wavefronts.Count);

                    var w = wavefrontAlgorithm.Wavefronts[0];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 270) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 355) < 1);

                    w = wavefrontAlgorithm.Wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 315) < 1);
                    Assert.AreEqual(360, w.ToAngle);

                    w = wavefrontAlgorithm.Wavefronts[2];
                    Assert.AreEqual(0, w.FromAngle);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 90) < 1);
                }
            }

            public class AddWavefrontIfValid : WithWavefrontAlgorithm
            {
                [Test]
                public void EqualAngleBetweenFromAndTo()
                {
                    Assert.True(wavefronts.IsEmpty());
                    wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices, 10, rootVertex, 10, 10);
                    Assert.True(wavefronts.IsEmpty());
                }

                [Test]
                public void NewWavefrontWouldBeInvalid()
                {
                    Assert.True(wavefronts.IsEmpty());
                    wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices, 10, rootVertex, 10, 11);
                    Assert.True(wavefronts.IsEmpty());
                }

                [Test]
                public void NewWavefrontAdded()
                {
                    Assert.True(wavefronts.IsEmpty());

                    wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices, 10, rootVertex, 190, 360);

                    Assert.AreEqual(1, wavefronts.Count);
                    var wavefront = wavefronts[0];
                    Assert.AreEqual(190, wavefront.FromAngle);
                    Assert.AreEqual(360, wavefront.ToAngle);
                    Assert.AreEqual(2, wavefront.RelevantVertices.Count);
                    Assert.AreEqual(rootVertex, wavefront.RootVertex);
                }
            }

            public class HandleNeighbors : WithWavefrontAlgorithm
            {
                [Test]
                public void NoNeighborToWest()
                {
                    var wavefront = Wavefront.New(0, 90, new Vertex(5, 0), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsTrue(createdWavefront);
                    Assert.IsTrue(Math.Abs(angleShadowFrom - 18.435) < 1);
                    Assert.IsTrue(Math.Abs(angleShadowTo - 33.69) < 1);

                    Assert.AreEqual(2, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.AreEqual(0, w.FromAngle);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 33.69) < 1);
                    Assert.AreEqual(1, w.RelevantVertices.Count);
                    Assert.AreEqual(multiVertexLineObstacle[2].ToPosition(), w.RelevantVertices[0].Position);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void EndOfLine_CreatesNew180DegreeWavefront()
                {
                    var wavefront = Wavefront.New(180, 270, new Vertex(multiVertexLineObstacle[1].ToPosition()),
                        wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[0];
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsTrue(createdWavefront);
                    Assert.IsNaN(angleShadowFrom);
                    Assert.IsNaN(angleShadowTo);

                    Assert.AreEqual(3, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 270) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 360) < 1);
                    Assert.AreEqual(2, w.RelevantVertices.Count);
                    Assert.AreEqual(vertex, w.RootVertex);

                    w = wavefronts[2];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 0) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 90) < 1);
                    Assert.AreEqual(2, w.RelevantVertices.Count);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void StartingFromEndOfLine_CreatesNewWavefront()
                {
                    var wavefront = Wavefront.New(90, 180, new Vertex(multiVertexLineObstacle[0].ToPosition()),
                        wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsTrue(createdWavefront);
                    Assert.IsNaN(angleShadowFrom);
                    Assert.IsNaN(angleShadowTo);

                    Assert.AreEqual(2, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 0) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 90) < 1);
                    Assert.AreEqual(1, w.RelevantVertices.Count);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void NoNeighborToEast()
                {
                    var wavefront = Wavefront.New(190, 350, new Vertex(8, 4.5), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsTrue(createdWavefront);
                    Assert.IsTrue(Math.Abs(angleShadowTo - 243.43) < 1);
                    Assert.IsTrue(Math.Abs(angleShadowFrom - 213.69) < 1);

                    Assert.AreEqual(2, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 213.69) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 270) < 1);
                    Assert.AreEqual(1, w.RelevantVertices.Count);
                    Assert.AreEqual(multiVertexLineObstacle[0].ToPosition(), w.RelevantVertices[0].Position);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void NeighborsOutsideWavefront_NotVisited()
                {
                    var wavefront = Wavefront.New(300, 330, new Vertex(8, 2), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];
                    Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsFalse(createdWavefront);
                    Assert.AreEqual(Double.NaN, angleShadowFrom);
                    Assert.AreEqual(Double.NaN, angleShadowTo);

                    Assert.AreEqual(1, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }

                [Test]
                public void NeighborsInsideWavefront_NotVisited()
                {
                    var wavefront = Wavefront.New(270, 360, new Vertex(8, 2), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];
                    Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                    Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsFalse(createdWavefront);
                    Assert.AreEqual(Double.NaN, angleShadowFrom);
                    Assert.AreEqual(Double.NaN, angleShadowTo);

                    Assert.AreEqual(1, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }

                [Test]
                public void NeighborsInsideWavefront_BothVisited()
                {
                    var wavefront = Wavefront.New(0, 270, new Vertex(6, 4), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsFalse(createdWavefront);
                    Assert.IsTrue(Math.Abs(angleShadowFrom - 90) < 1);
                    Assert.IsTrue(Math.Abs(angleShadowTo - 180) < 1);

                    Assert.AreEqual(1, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }

                [Test]
                public void FirstVertexHasNeighbors_NotCastingShadow()
                {
                    var vertices = new List<Vertex>();
                    vertices.AddRange(wavefrontAlgorithm.Vertices);
                    vertices.Add(new Vertex(5, 2.5));
                    var wavefront = Wavefront.New(180, 270, new Vertex(7.5, 3.5), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.IsTrue(createdWavefront);
                    Assert.IsNaN(angleShadowFrom);
                    Assert.IsNaN(angleShadowTo);

                    Assert.AreEqual(2, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 225) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 270) < 1);
                    Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);
                }

                [Test]
                public void InnerCornerOnLine_NotCreatingNewWavefront()
                {
                    var wavefront = Wavefront.New(0, 90, multiVertexLineVertices[0], wavefrontAlgorithm.Vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.False(createdWavefront);
                    Assert.IsNaN(angleShadowFrom);
                    Assert.IsNaN(angleShadowTo);

                    Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(wavefront, wavefrontAlgorithm.Wavefronts[0]);
                }

                [Test]
                public void InnerCorner_NotCreatingNewWavefront()
                {
                    var wavefront = Wavefront.New(0, 90,
                        new Vertex(6.75, 3.25), wavefrontAlgorithm.Vertices,
                        1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    var vertex = multiVertexLineVertices[1];

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                        out var angleShadowTo, out var createdWavefront);

                    Assert.False(createdWavefront);
                    Assert.IsNaN(angleShadowFrom);
                    Assert.IsNaN(angleShadowTo);

                    Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(wavefront, wavefrontAlgorithm.Wavefronts[0]);
                }
            }

            public class AddNewWavefront : WithWavefrontAlgorithm
            {
                [Test]
                public void OneWavefrontAdded()
                {
                    Assert.True(wavefronts.IsEmpty());

                    wavefrontAlgorithm.AddNewWavefront(wavefrontAlgorithm.Vertices, rootVertex, 10, 190, 350);

                    Assert.AreEqual(1, wavefronts.Count);
                    var wavefront = wavefronts[0];
                    Assert.AreEqual(190, wavefront.FromAngle);
                    Assert.AreEqual(350, wavefront.ToAngle);
                    Assert.AreEqual(2, wavefront.RelevantVertices.Count);
                    Assert.AreEqual(rootVertex, wavefront.RootVertex);
                }

                [Test]
                public void AngleRangeExceedsZeroDegree()
                {
                    Assert.True(wavefronts.IsEmpty());

                    wavefrontAlgorithm.AddNewWavefront(wavefrontAlgorithm.Vertices, rootVertex, 10, 190, 90);

                    Assert.AreEqual(2, wavefronts.Count);

                    var wavefront = wavefronts[0];
                    Assert.AreEqual(190, wavefront.FromAngle);
                    Assert.AreEqual(360, wavefront.ToAngle);
                    Assert.AreEqual(2, wavefront.RelevantVertices.Count);
                    Assert.AreEqual(rootVertex, wavefront.RootVertex);

                    wavefront = wavefronts[1];
                    Assert.AreEqual(0, wavefront.FromAngle);
                    Assert.AreEqual(90, wavefront.ToAngle);
                    Assert.AreEqual(3, wavefront.RelevantVertices.Count);
                    Assert.AreEqual(rootVertex, wavefront.RootVertex);
                }
            }

            public class TrajectoryCollidesWithObstacle : WithWavefrontAlgorithm
            {
                [Test]
                public void NotVisible_BehindSimpleObstacle()
                {
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(3, 7),
                        Position.CreateGeoPosition(1, 7)));
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(3, 0),
                        Position.CreateGeoPosition(1.5, 11)));

                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(1, 7),
                        Position.CreateGeoPosition(3, 7)));
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(1.5, 11),
                        Position.CreateGeoPosition(3, 0)));
                }

                [Test]
                public void NotVisible_BehindMultiVertexObstacle()
                {
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(8, 3.5),
                        Position.CreateGeoPosition(6, 3.5)));
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(6.5, 2),
                        Position.CreateGeoPosition(6.5, 4)));
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(8, 5),
                        Position.CreateGeoPosition(5, 0)));

                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(6, 3.5),
                        Position.CreateGeoPosition(8, 3.5)));
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(6.5, 4),
                        Position.CreateGeoPosition(6.5, 2)));
                    Assert.True(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(5, 0),
                        Position.CreateGeoPosition(8, 5)));
                }

                [Test]
                public void Visible()
                {
                    Assert.False(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(1, 1),
                        Position.CreateGeoPosition(2, 2)));
                    Assert.False(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(4, 7),
                        Position.CreateGeoPosition(3, 7)));
                    Assert.False(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(3, 6),
                        Position.CreateGeoPosition(3, 7)));
                    Assert.False(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(2, 5),
                        Position.CreateGeoPosition(3, 7)));
                    Assert.False(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(7, 5),
                        Position.CreateGeoPosition(6.5, 3.5)));
                    Assert.False(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(3, 5),
                        Position.CreateGeoPosition(1, 5)));
                    Assert.False(wavefrontAlgorithm.TrajectoryCollidesWithObstacle(Position.CreateGeoPosition(1, 5),
                        Position.CreateGeoPosition(3, 5)));
                }
            }
        }

        public class Route
        {
            static WavefrontAlgorithm wavefrontAlgorithm;
            static LineString multiVertexLineObstacle;
            static LineString rotatedLineObstacle;

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
                var obstacles = new List<NetTopologySuite.Geometries.Geometry>();
                obstacles.Add(multiVertexLineObstacle);
                obstacles.Add(rotatedLineObstacle);

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void RouteDirectlyToTarget()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 1.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 1.5);

                var waypoints = wavefrontAlgorithm.Route(sourceVertex, targetVertex);

                Assert.Contains(sourceVertex, waypoints);
                Assert.Contains(targetVertex, waypoints);
                Assert.AreEqual(2, waypoints.Count);
            }

            [Test]
            public void RouteOverOneVertex()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 1.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 2.5);

                var waypoints = wavefrontAlgorithm.Route(sourceVertex, targetVertex);

                Assert.AreEqual(3, waypoints.Count);
                Assert.Contains(sourceVertex, waypoints);
                Assert.Contains(Position.CreateGeoPosition(multiVertexLineObstacle[0].X, multiVertexLineObstacle[0].Y),
                    waypoints);
                Assert.Contains(targetVertex, waypoints);
            }

            [Test]
            public void RouteOverTwoVertices()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 2.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 2.5);

                var waypoints = wavefrontAlgorithm.Route(sourceVertex, targetVertex);

                Assert.Contains(sourceVertex, waypoints);
                Assert.Contains(Position.CreateGeoPosition(multiVertexLineObstacle[1].X, multiVertexLineObstacle[1].Y),
                    waypoints);
                Assert.Contains(Position.CreateGeoPosition(multiVertexLineObstacle[0].X, multiVertexLineObstacle[0].Y),
                    waypoints);
                Assert.Contains(targetVertex, waypoints);
                Assert.AreEqual(4, waypoints.Count);
            }

            [Test]
            public void RouteOverTwoVertices_SourceAndTargetSwitched()
            {
                // Switched source and target
                var sourceVertex = Position.CreateGeoPosition(1.5, 2.5);
                var targetVertex = Position.CreateGeoPosition(3.5, 2.5);

                var waypoints = wavefrontAlgorithm.Route(sourceVertex, targetVertex);

                Assert.Contains(sourceVertex, waypoints);
                Assert.Contains(Position.CreateGeoPosition(multiVertexLineObstacle[0].X, multiVertexLineObstacle[0].Y),
                    waypoints);
                Assert.Contains(Position.CreateGeoPosition(multiVertexLineObstacle[1].X, multiVertexLineObstacle[1].Y),
                    waypoints);
                Assert.Contains(targetVertex, waypoints);
                Assert.AreEqual(4, waypoints.Count);
            }

            [Test]
            public void RouteFromInsideCorner()
            {
                // In between those is a corner with two neighbors, so that corner cannot be used.
                var sourceVertex = Position.CreateGeoPosition(6, 3);
                var targetVertex = Position.CreateGeoPosition(7, 3);

                var waypoints = wavefrontAlgorithm.Route(sourceVertex, targetVertex);

                Assert.Contains(sourceVertex, waypoints);
                Assert.Contains(Position.CreateGeoPosition(rotatedLineObstacle[0].X, rotatedLineObstacle[0].Y),
                    waypoints);
                Assert.Contains(Position.CreateGeoPosition(rotatedLineObstacle[1].X, rotatedLineObstacle[1].Y),
                    waypoints);
                Assert.Contains(targetVertex, waypoints);
                Assert.AreEqual(4, waypoints.Count);
            }
        }
    }
}