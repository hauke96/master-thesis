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
                var obstacles = new List<NetTopologySuite.Geometries.Geometry>();
                obstacles.Add(multiVertexLineObstacle);
                obstacles.Add(simpleLineObstacle);

                wavefronts = new List<Wavefront>();
                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles, wavefronts);
                rootVertex = new Vertex(Position.CreateGeoPosition(5, 2));
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
                    var nextVertex = new Vertex(multiVertexLineObstacle.Coordinates[0]);
                    vertices.Add(nextVertex);
                    var wavefront = Wavefront.New(0, 90, new Vertex(5, 2), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    wavefrontAlgorithm.PositionToPredecessor[nextVertex.Position] = Position.CreateGeoPosition(1, 1);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(0, wavefront.RelevantVertices.Count);
                }

                [Test]
                public void TargetReached()
                {
                    var vertices = new List<Vertex>();
                    var nextVertex = new Vertex(targetPosition);
                    vertices.Add(nextVertex);
                    var wavefront = Wavefront.New(0, 90, new Vertex(5, 2), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    wavefrontAlgorithm.PositionToPredecessor[nextVertex.Position] = Position.CreateGeoPosition(1, 1);

                    wavefrontAlgorithm.ProcessNextEvent(targetPosition);

                    Assert.AreEqual(wavefront.RootVertex.Position,
                        wavefrontAlgorithm.PositionToPredecessor[targetPosition]);
                    Assert.AreEqual(0, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(0, wavefront.RelevantVertices.Count);
                }
            }

            public class WithMultiVertexLineFullyInsideWavefront : WithWavefrontAlgorithm
            {
                Wavefront wavefront;
                Vertex nextVertex;
                Position targetPosition;

                [SetUp]
                public void setup()
                {
                    var vertices = new List<Vertex>();
                    nextVertex = new Vertex(multiVertexLineObstacle.Coordinates[0], multiVertexLineObstacle);
                    vertices.Add(nextVertex);
                    vertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[2], multiVertexLineObstacle));
                    vertices.Add(new Vertex(new Coordinate(5, 6), simpleLineObstacle));
                    wavefront = Wavefront.New(0, 350, new Vertex(5, 0), vertices, 1)!;
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
                    Assert.IsFalse(wavefrontAlgorithm.Wavefronts.Contains(wavefront));
                    Assert.AreEqual(3, wavefrontAlgorithm.Wavefronts.Count);

                    var w = wavefrontAlgorithm.Wavefronts[0];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 18.43) < 1);
                    Assert.AreEqual(90, w.ToAngle);

                    w = wavefrontAlgorithm.Wavefronts[1];
                    Assert.AreEqual(0, w.FromAngle);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 18.43) < 1);

                    w = wavefrontAlgorithm.Wavefronts[2];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 33.69) < 1);
                    Assert.AreEqual(350, w.ToAngle);
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
                    var vertices = new List<Vertex>();
                    nextVertex = new Vertex(multiVertexLineObstacle.Coordinates[0], multiVertexLineObstacle);
                    vertices.Add(nextVertex);
                    vertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[1], multiVertexLineObstacle));
                    vertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[2], multiVertexLineObstacle));
                    vertices.Add(new Vertex(simpleLineObstacle.Coordinates[0], simpleLineObstacle));
                    vertices.Add(new Vertex(simpleLineObstacle.Coordinates[1], simpleLineObstacle));
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

                    wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices, 10, rootVertex, 190, 0);

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
                    var vertex = new Vertex(multiVertexLineObstacle[1], multiVertexLineObstacle);
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowRight,
                        out var angleShadowLeft, out var createdWavefront);

                    Assert.IsTrue(createdWavefront);
                    Assert.IsTrue(Math.Abs(angleShadowRight - 18.435) < 1);
                    Assert.IsTrue(Math.Abs(angleShadowLeft - 33.69) < 1);

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
                public void NoNeighborToEast()
                {
                    var wavefront = Wavefront.New(190, 350, new Vertex(8, 4.5), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle[1], multiVertexLineObstacle);
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowRight,
                        out var angleShadowLeft, out var createdWavefront);

                    Assert.IsTrue(createdWavefront);
                    Assert.IsTrue(Math.Abs(angleShadowLeft - 243.43) < 1);
                    Assert.IsTrue(Math.Abs(angleShadowRight - 213.69) < 1);

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
                    var vertex = new Vertex(multiVertexLineObstacle[1], multiVertexLineObstacle);
                    Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowRight,
                        out var angleShadowLeft, out var createdWavefront);

                    Assert.IsFalse(createdWavefront);
                    Assert.AreEqual(Double.NaN, angleShadowRight);
                    Assert.AreEqual(Double.NaN, angleShadowLeft);

                    Assert.AreEqual(1, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }

                [Test]
                public void NeighborsInsideWavefront_NotVisited()
                {
                    var wavefront = Wavefront.New(270, 360, new Vertex(8, 2), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle[1], multiVertexLineObstacle);
                    Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                    Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowRight,
                        out var angleShadowLeft, out var createdWavefront);

                    Assert.IsFalse(createdWavefront);
                    Assert.AreEqual(Double.NaN, angleShadowRight);
                    Assert.AreEqual(Double.NaN, angleShadowLeft);

                    Assert.AreEqual(1, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }
                
                [Test]
                public void NeighborsInsideWavefront_BothVisited()
                {
                    var wavefront = Wavefront.New(0, 270, new Vertex(6, 4), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle[1], multiVertexLineObstacle);
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                    wavefront.RemoveNextVertex();
                    Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                    Assert.AreEqual(vertex, wavefront.GetNextVertex());

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowRight,
                        out var angleShadowLeft, out var createdWavefront);

                    Assert.IsFalse(createdWavefront);
                    Assert.IsTrue(Math.Abs(angleShadowRight - 90) < 1);
                    Assert.IsTrue(Math.Abs(angleShadowLeft - 180) < 1);

                    Assert.AreEqual(1, wavefronts.Count);
                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
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
        }

        public class Route
        {
            static WavefrontAlgorithm wavefrontAlgorithm;
            static LineString multiVertexLineObstacle;

            [SetUp]
            public void Setup()
            {
                multiVertexLineObstacle = new LineString(new[]
                {
                    new Coordinate(2, 2),
                    new Coordinate(3, 2),
                    new Coordinate(3, 5),
                });
                var obstacles = new List<NetTopologySuite.Geometries.Geometry>();
                obstacles.Add(multiVertexLineObstacle);

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
            public void RouteOverOneEdge()
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
            public void RouteOverTwoEdges()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 2.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 2.5);

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
            public void RouteOverTwoEdges_SourceAndTargetSwitched()
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
        }
    }
}