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
        [Test]
        public void IsEndOfGeometry()
        {
            Assert.True(WavefrontAlgorithm.IsEndOfGeometry(null, new Coordinate(1, 2)));
            Assert.True(WavefrontAlgorithm.IsEndOfGeometry(new Coordinate(1, 2), null));

            Assert.False(WavefrontAlgorithm.IsEndOfGeometry(new Coordinate(0, 1), new Coordinate(1, 2)));
            Assert.False(WavefrontAlgorithm.IsEndOfGeometry(null, null));
        }

        public class WithWavefrontAlgorithm
        {
            static WavefrontAlgorithm wavefrontAlgorithm;
            static Vertex rootVertex;
            static List<Wavefront> wavefronts;
            static LineString multiVertexLineObstacle;
            static LineString simpleineObstacle;

            [SetUp]
            public void Setup()
            {
                multiVertexLineObstacle = new LineString(new[]
                {
                    new Coordinate(6, 3),
                    new Coordinate(7, 3),
                    new Coordinate(8, 4),
                });
                simpleineObstacle = new LineString(new[]
                {
                    new Coordinate(2, 5),
                    new Coordinate(2, 10)
                });
                var obstacles = new List<NetTopologySuite.Geometries.Geometry>();
                obstacles.Add(multiVertexLineObstacle);
                obstacles.Add(simpleineObstacle);

                wavefronts = new List<Wavefront>();

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles, wavefronts);

                rootVertex = new Vertex(Position.CreateGeoPosition(5, 2));
            }

            public class ProcessNextWavefront : WithWavefrontAlgorithm
            {
                [Test]
                public void WavefrontHasNoNextVertex()
                {
                    var vertices = new List<Vertex>();
                    vertices.Add(new Vertex(6.5, 3.1));
                    var wavefront = Wavefront.New(0, 90, new Vertex(1, 1), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);
                    // Remove last remaining vertex
                    wavefront.RemoveNextVertex();

                    wavefrontAlgorithm.ProcessNextEvent();

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

                    wavefrontAlgorithm.ProcessNextEvent();

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

                    wavefrontAlgorithm.ProcessNextEvent();

                    Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                    Assert.AreEqual(0, wavefront.RelevantVertices.Count);
                }
            }

            public class WithMultiVertexLineFullyInsideWavefront : WithWavefrontAlgorithm
            {
                Wavefront wavefront;
                Vertex nextVertex;

                [SetUp]
                public void setup()
                {
                    var vertices = new List<Vertex>();
                    nextVertex = new Vertex(multiVertexLineObstacle.Coordinates[0], multiVertexLineObstacle);
                    vertices.Add(nextVertex);
                    vertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[2], multiVertexLineObstacle));
                    vertices.Add(new Vertex(new Coordinate(5, 6), simpleineObstacle));
                    wavefront = Wavefront.New(0, 350, new Vertex(5, 0), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);

                    wavefrontAlgorithm.ProcessNextEvent();
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

                [SetUp]
                public void setup()
                {
                    var vertices = new List<Vertex>();
                    nextVertex = new Vertex(multiVertexLineObstacle.Coordinates[0], multiVertexLineObstacle);
                    vertices.Add(nextVertex);
                    vertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[1], multiVertexLineObstacle));
                    vertices.Add(new Vertex(multiVertexLineObstacle.Coordinates[2], multiVertexLineObstacle));
                    vertices.Add(new Vertex(simpleineObstacle.Coordinates[0], simpleineObstacle));
                    vertices.Add(new Vertex(simpleineObstacle.Coordinates[1], simpleineObstacle));
                    // Add wavefront close to the next vertex
                    wavefront = Wavefront.New(270, 355, new Vertex(6.2, 2.8), vertices, 1)!;
                    wavefrontAlgorithm.Wavefronts.Add(wavefront);

                    wavefrontAlgorithm.ProcessNextEvent();
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
                    Assert.IsTrue(Math.Abs(w.FromAngle - 315) < 1);
                    Assert.AreEqual(360, w.ToAngle);

                    w = wavefrontAlgorithm.Wavefronts[1];
                    Assert.AreEqual(0, w.FromAngle);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 90) < 1);

                    w = wavefrontAlgorithm.Wavefronts[2];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 270) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 315) < 1);
                }
            }

            public class AdjustWavefront : WithWavefrontAlgorithm
            {
                Wavefront wavefront;

                [SetUp]
                public void Setup()
                {
                    wavefront = Wavefront.New(10, 350, rootVertex, wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                }

                [Test]
                public void FromLowerToAngle()
                {
                    Assert.AreEqual(1, wavefronts.Count);
                    Assert.True(wavefronts.Contains(wavefront));

                    wavefrontAlgorithm.AdjustWavefront(wavefrontAlgorithm.Vertices, rootVertex, 5, 50, 250, wavefront);

                    Assert.AreEqual(1, wavefronts.Count);
                    Assert.False(wavefronts.Contains(wavefront));
                }

                [Test]
                public void FromLargerToAngle()
                {
                    Assert.AreEqual(1, wavefronts.Count);
                    Assert.True(wavefronts.Contains(wavefront));

                    wavefrontAlgorithm.AdjustWavefront(wavefrontAlgorithm.Vertices, rootVertex, 5, 250, 50, wavefront);

                    Assert.AreEqual(2, wavefronts.Count);
                    Assert.AreEqual(250, wavefronts[0].FromAngle);
                    Assert.AreEqual(360, wavefronts[0].ToAngle);
                    Assert.AreEqual(0, wavefronts[1].FromAngle);
                    Assert.AreEqual(50, wavefronts[1].ToAngle);
                    Assert.False(wavefronts.Contains(wavefront));
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
                    var vertex = new Vertex(new Coordinate(6, 3), multiVertexLineObstacle);

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleWavefrontRight,
                        out var angleWavefrontLeft);

                    // Not exactly 45° due to ellipsoid stuff(?) and/or float calculations
                    Assert.IsTrue(Math.Abs(angleWavefrontRight - 18.435) < 1);
                    Assert.IsTrue(Math.Abs(angleWavefrontLeft - 33.69) < 1);
                    Assert.AreEqual(2, wavefronts.Count);

                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 18.435) < 1);
                    Assert.AreEqual(90, w.ToAngle);
                    Assert.AreEqual(2, w.RelevantVertices.Count);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void NoNeighborToEast()
                {
                    var wavefront = Wavefront.New(225, 355, new Vertex(9, 3), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle.Coordinates[2], multiVertexLineObstacle);

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleWavefrontRight,
                        out var angleWavefrontLeft);

                    // Not exactly 45° due to ellipsoid stuff(?) and/or float calculations
                    Assert.IsTrue(Math.Abs(angleWavefrontRight - 270) < 1);
                    Assert.IsTrue(Math.Abs(angleWavefrontLeft - 315) < 1);
                    Assert.AreEqual(2, wavefronts.Count);

                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 225) < 1);
                    Assert.IsTrue(Math.Abs(w.ToAngle - 315) < 1);
                    Assert.AreEqual(3, w.RelevantVertices.Count);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void NeighborOutsideWavefront()
                {
                    var wavefront = Wavefront.New(225, 355, new Vertex(Position.CreateGeoPosition(8, 1)),
                        wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle.Coordinates[1], multiVertexLineObstacle);

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleWavefrontRight,
                        out var angleWavefrontLeft);

                    Assert.IsTrue(Math.Abs(angleWavefrontRight - 315) < 1);
                    Assert.AreEqual(wavefront.ToAngle, angleWavefrontLeft);
                    Assert.AreEqual(1, wavefronts.Count);

                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }

                [Test]
                public void NeighborsInsideWavefront()
                {
                    var wavefront = Wavefront.New(270, 355, new Vertex(9, 1), wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle.Coordinates[1], multiVertexLineObstacle);

                    wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleWavefrontRight,
                        out var angleWavefrontLeft);

                    // Not exactly 45° due to ellipsoid stuff(?) and/or float calculations
                    Assert.IsTrue(Math.Abs(angleWavefrontRight - 303.69) < 1);
                    Assert.IsTrue(Math.Abs(angleWavefrontLeft - 341.565) < 1);
                    Assert.AreEqual(1, wavefronts.Count);

                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }
            }

            public class HandleNeighborVertex : WithWavefrontAlgorithm
            {
                [Test]
                public void NoNeighborToWest()
                {
                    var wavefront = Wavefront.New(10, 350, rootVertex, wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(new Coordinate(6, 3), multiVertexLineObstacle);

                    var wavefrontEdgeAngle = wavefrontAlgorithm.HandleNeighborVertex(wavefront, null, vertex);

                    // Not exactly 45° due to ellipsoid stuff(?) and/or float calculations
                    Assert.IsTrue(Math.Abs(wavefrontEdgeAngle - 45) < 1);
                    Assert.AreEqual(2, wavefronts.Count);

                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.AreEqual(wavefrontEdgeAngle, w.FromAngle);
                    Assert.AreEqual(90, w.ToAngle);
                    Assert.AreEqual(2, w.RelevantVertices.Count);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void NoNeighborToEast()
                {
                    var wavefront = Wavefront.New(280, 355, new Vertex(Position.CreateGeoPosition(9, 3)),
                        wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle.Coordinates[2], multiVertexLineObstacle);

                    var wavefrontEdgeAngle = wavefrontAlgorithm.HandleNeighborVertex(wavefront, null, vertex);

                    // Not exactly 315° due to ellipsoid stuff(?) and/or float calculations
                    Assert.IsTrue(Math.Abs(wavefrontEdgeAngle - 315) < 1);
                    Assert.AreEqual(2, wavefronts.Count);

                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);

                    w = wavefronts[1];
                    Assert.IsTrue(Math.Abs(w.FromAngle - 225) < 1);
                    Assert.AreEqual(wavefrontEdgeAngle, w.ToAngle);
                    Assert.AreEqual(3, w.RelevantVertices.Count);
                    Assert.AreEqual(vertex, w.RootVertex);
                }

                [Test]
                public void NeighborOutsideWavefront()
                {
                    var wavefront = Wavefront.New(225, 355, new Vertex(Position.CreateGeoPosition(8, 1)),
                        wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle.Coordinates[1], multiVertexLineObstacle);

                    var wavefrontEdgeAngle =
                        wavefrontAlgorithm.HandleNeighborVertex(wavefront, multiVertexLineObstacle.Coordinates[2],
                            vertex);

                    Assert.AreEqual(wavefront.ToAngle, wavefrontEdgeAngle);
                    Assert.AreEqual(1, wavefronts.Count);

                    var w = wavefronts[0];
                    Assert.AreEqual(wavefront, w);
                }

                [Test]
                public void NeighborInsideWavefront()
                {
                    var wavefront = Wavefront.New(225, 355, new Vertex(Position.CreateGeoPosition(8, 1)),
                        wavefrontAlgorithm.Vertices, 10)!;
                    wavefronts.Add(wavefront);
                    var vertex = new Vertex(multiVertexLineObstacle.Coordinates[1], multiVertexLineObstacle);

                    var wavefrontEdgeAngle =
                        wavefrontAlgorithm.HandleNeighborVertex(wavefront, multiVertexLineObstacle.Coordinates[0],
                            vertex);

                    // Not exactly 315° due to ellipsoid stuff(?) and/or float calculations
                    Assert.IsTrue(Math.Abs(wavefrontEdgeAngle - 315) < 1);
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
            public void RouteOverTwoEdgesReverse()
            {
                // Switched source and target
                var targetVertex = Position.CreateGeoPosition(3.5, 2.5);
                var sourceVertex = Position.CreateGeoPosition(1.5, 2.5);

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