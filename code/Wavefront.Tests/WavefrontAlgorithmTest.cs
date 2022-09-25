using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using Mars.Common;
using Mars.Common.Collections;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Tests
{
    public class WavefrontAlgorithmTest
    {
        private static readonly double FLOAT_TOLERANCE = 0.01;

        public class ProcessNextEvent : WavefrontTestHelper.WithWavefrontAlgorithm
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
                var wavefront = Wavefront.New(0, 90, new Vertex(1, 1), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                // Remove last remaining vertex
                wavefront.RemoveNextVertex();

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(0, wavefrontAlgorithm.Wavefronts.Count);
            }

            [Test]
            public void EventIsNotValid()
            {
                var vertices = new List<Vertex>();
                vertices.Add(new Vertex(6.5, 3.1));
                vertices.Add(multiVertexLineVertices[1]);
                // Between but slightly below the multi-vertex-line
                var wavefront = Wavefront.New(0, 90, new Vertex(6.5, 2.9), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(1, wavefront.RelevantVertices.Count);
            }

            [Test]
            public void EventVertexHasAlreadyBeenProcessed()
            {
                var vertices = new List<Vertex>();
                var nextVertex = multiVertexLineVertices[0];
                vertices.Add(nextVertex);
                vertices.Add(multiVertexLineVertices[1]);
                var wavefront = Wavefront.New(0, 90, new Vertex(5, 2), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                wavefrontAlgorithm.WaypointToPredecessor[new Waypoint(nextVertex.Position, 0, 0)] =
                    new Waypoint(Position.CreateGeoPosition(1, 1), 0, 0);
                wavefrontAlgorithm.WavefrontRootPredecessor.Add(new Waypoint(nextVertex.Position, 0, 0), null);
                wavefrontAlgorithm.WavefrontRootToWaypoint.Add(nextVertex.Position,
                    wavefrontAlgorithm.WavefrontRootPredecessor.First().Key);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(1, wavefront.RelevantVertices.Count);
            }

            [Test]
            public void FirstVertexHasNeighbors_NotCastingShadow()
            {
                var vertices = new List<Vertex>();
                vertices.AddRange(wavefrontAlgorithm.Vertices);
                vertices.Add(new Vertex(5, 2.5));
                var wavefront = Wavefront.New(180, 270, new Vertex(7.5, 3.5), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                Assert.AreEqual(multiVertexLineVertices[1].Position, wavefront.GetNextVertex()?.Position);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);

                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavefronts[0];
                Assert.AreEqual(wavefront, w);

                w = wavefronts[1];
                Assert.AreEqual(225, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(270, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);
            }

            [Test]
            public void TargetReached()
            {
                var vertices = new List<Vertex>();
                var sourceVertex = multiVertexLineVertices[0];
                var targetVertex = new Vertex(targetPosition);
                vertices.Add(targetVertex);

                var wavefront = Wavefront.New(0, 90, sourceVertex, vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);

                var rootWaypoint = new Waypoint(wavefront.RootVertex.Position, 0, 0);
                wavefrontAlgorithm.WaypointToPredecessor[rootWaypoint] = null;
                wavefrontAlgorithm.PositionToWaypoint[rootWaypoint.Position] = rootWaypoint;

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var targetWaypoint =
                    wavefrontAlgorithm.WaypointToPredecessor.Keys.First(k => k.Position.Equals(targetPosition));
                Assert.NotNull(wavefrontAlgorithm.WaypointToPredecessor[targetWaypoint]);
                Assert.AreEqual(wavefront.RootVertex.Position,
                    wavefrontAlgorithm.WaypointToPredecessor[targetWaypoint].Position);
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

                var sourceVertex = vertices.ElementAt(0);
                var vertex = vertices.ElementAt(2);

                var wavefront = Wavefront.New(269.992367019, 180.00763298898323, sourceVertex, vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);

                wavefront.RemoveNextVertex();
                Assert.IsTrue(wavefront.HasBeenVisited(vertices.ElementAt(1).Position));
                wavefront.RemoveNextVertex();
                Assert.IsTrue(wavefront.HasBeenVisited(vertices.ElementAt(3).Position));
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavefronts[0];
                Assert.AreEqual(0, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(127.1, w.ToAngle, FLOAT_TOLERANCE);

                w = wavefronts[1];
                Assert.AreEqual(wavefront.FromAngle, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(360, w.ToAngle, FLOAT_TOLERANCE);
            }

            [Test]
            public void FromAndToWithinShadowArea()
            {
                var wavefront = Wavefront.New(10, 350, new Vertex(6.5, 2.5), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;
                var vertex = multiVertexLineVertices[1];
                wavefront.RemoveNextVertex();
                Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(vertex, wavefront.GetNextVertex());
                wavefrontAlgorithm.AddWavefront(wavefront);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.AreEqual(2, wavefronts.Count);
                Assert.AreEqual(0, wavefronts[0].FromAngle);
                Assert.AreEqual(45, wavefronts[0].ToAngle);
                Assert.AreEqual(45, wavefronts[1].FromAngle);
                Assert.AreEqual(315, wavefronts[1].ToAngle);
            }
        }

        public class WithMultiVertexLineFullyInsideWavefront : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            Wavefront wavefront;
            Vertex nextVertex;
            Position targetPosition;

            [SetUp]
            public void Setup()
            {
                nextVertex = multiVertexLineVertices[0];
                wavefront = Wavefront.New(0, 350, new Vertex(5, 0), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                targetPosition = Position.CreateGeoPosition(10, 10);

                var rootWaypoint = new Waypoint(wavefront.RootVertex.Position, 0, 0);
                wavefrontAlgorithm.WaypointToPredecessor[rootWaypoint] = null;
                wavefrontAlgorithm.PositionToWaypoint[rootWaypoint.Position] = rootWaypoint;

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());
            }

            [Test]
            public void SetsPredecessorCorrectly()
            {
                var waypoint =
                    wavefrontAlgorithm.WaypointToPredecessor.Keys.First(k => k.Position.Equals(nextVertex.Position));
                Assert.NotNull(wavefrontAlgorithm.WaypointToPredecessor[waypoint]);
                Assert.AreEqual(wavefront.RootVertex.Position,
                    wavefrontAlgorithm.WaypointToPredecessor[waypoint].Position);
            }

            [Test]
            public void RemovesNextVertexFromWavefront()
            {
                Assert.IsFalse(wavefront.RelevantVertices.Contains(nextVertex));
            }

            [Test]
            public void NotCastingShadow_NotRemovingOldWavefront()
            {
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.IsTrue(wavefronts.Contains(wavefront));
                Assert.AreEqual(2, wavefronts.Count);

                var w = wavefronts[0];
                Assert.AreEqual(wavefront, w);

                w = wavefronts[1];
                Assert.AreEqual(18.43, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle);
                Assert.AreEqual(multiVertexLineObstacle[0], w.RootVertex.Coordinate);
            }

            [Test]
            public void NextVertexCastingShadow()
            {
                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.IsFalse(wavefronts.Contains(wavefront));
                Assert.AreEqual(3, wavefronts.Count);

                var w = wavefronts[0];
                Assert.AreEqual(18.43, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle);
                Assert.AreEqual(multiVertexLineObstacle[0], w.RootVertex.Coordinate);

                w = wavefronts[1];
                Assert.AreEqual(0, w.FromAngle);
                Assert.AreEqual(33.69, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);

                w = wavefronts[2];
                Assert.AreEqual(33.69, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(350, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(wavefront.RootVertex, w.RootVertex);
            }
        }

        public class NewWavefrontExceedingZeroDegree : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            Wavefront wavefront;
            Vertex nextVertex;
            Position targetPosition;

            [SetUp]
            public void Setup()
            {
                nextVertex = multiVertexLineVertices[0];
                // Add wavefront close to the next vertex
                wavefront = Wavefront.New(270, 355, new Vertex(6.2, 2.8), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                targetPosition = Position.CreateGeoPosition(10, 10);

                var rootWaypoint = new Waypoint(wavefront.RootVertex.Position, 0, 0);
                wavefrontAlgorithm.WaypointToPredecessor[rootWaypoint] = null;
                wavefrontAlgorithm.PositionToWaypoint[rootWaypoint.Position] = rootWaypoint;

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());
            }

            [Test]
            public void SetsPredecessorCorrectly()
            {
                var waypoint =
                    wavefrontAlgorithm.WaypointToPredecessor.Keys.First(k => k.Position.Equals(nextVertex.Position));
                Assert.NotNull(wavefrontAlgorithm.WaypointToPredecessor[waypoint]);
                Assert.AreEqual(wavefront.RootVertex.Position,
                    wavefrontAlgorithm.WaypointToPredecessor[waypoint].Position);
            }

            [Test]
            public void RemovesNextVertexFromWavefront()
            {
                Assert.IsFalse(wavefront.RelevantVertices.Contains(nextVertex));
            }

            [Test]
            public void ReplacesOriginalWavefront()
            {
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.Contains(wavefront, wavefronts);
                Assert.AreEqual(3, wavefronts.Count);

                var w = wavefronts[0];
                Assert.AreEqual(0, w.FromAngle);
                Assert.AreEqual(90, w.ToAngle, FLOAT_TOLERANCE);

                w = wavefronts[1];
                Assert.AreEqual(270, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(355, w.ToAngle, FLOAT_TOLERANCE);

                w = wavefronts[2];
                Assert.AreEqual(315.0, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(360, w.ToAngle);
            }
        }

        public class AddWavefrontIfValid : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            [Test]
            public void EqualAngleBetweenFromAndTo()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());
                wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices.ToList(), 10, rootVertex, 10,
                    10, false);
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());
            }

            [Test]
            public void NewWavefrontWouldBeInvalid()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());
                wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices.ToList(), 10, rootVertex, 10,
                    11, false);
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());
            }

            [Test]
            public void NewWavefrontAdded()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());

                wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices.ToList(), 10, rootVertex, 190,
                    360, false);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var wavefront = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(190, wavefront.FromAngle);
                Assert.AreEqual(360, wavefront.ToAngle);
                Assert.AreEqual(2, wavefront.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavefront.RootVertex);
            }
        }

        public class HandleNeighbors : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            [Test]
            public void NoNeighborToWest()
            {
                var wavefront = Wavefront.New(0, 90, new Vertex(5, 0), wavefrontAlgorithm.Vertices.ToList(), 10,
                    false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];
                wavefront.RemoveNextVertex();
                Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.AreEqual(18.435, angleShadowFrom, FLOAT_TOLERANCE);
                Assert.AreEqual(33.69, angleShadowTo, FLOAT_TOLERANCE);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavefronts[0];
                Assert.AreEqual(wavefront, w);

                w = wavefronts[1];
                Assert.AreEqual(0, w.FromAngle);
                Assert.AreEqual(33.69, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(1, w.RelevantVertices.Count);
                Assert.AreEqual(multiVertexLineObstacle[2].ToPosition(), w.RelevantVertices.First().Position);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void EndOfLine_CreatesNew180DegreeWavefront()
            {
                var wavefront = Wavefront.New(180, 270, new Vertex(multiVertexLineObstacle[1].ToPosition()),
                    wavefrontAlgorithm.Vertices.ToList(), 10, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[0];
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(3, wavefrontAlgorithm.Wavefronts.Count);
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavefronts[0];
                Assert.AreEqual(wavefront, w);

                w = wavefronts[1];
                Assert.AreEqual(0, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(2, w.RelevantVertices.Count);
                Assert.AreEqual(vertex, w.RootVertex);

                w = wavefronts[2];
                Assert.AreEqual(270, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(360, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(2, w.RelevantVertices.Count);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void StartingFromEndOfLine_CreatesNewWavefront()
            {
                var wavefront = Wavefront.New(90, 180, new Vertex(multiVertexLineObstacle[0].ToPosition()),
                    wavefrontAlgorithm.Vertices.ToList(), 10, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavefronts[0];
                Assert.AreEqual(wavefront, w);

                w = wavefronts[1];
                Assert.AreEqual(0, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(1, w.RelevantVertices.Count);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void NoNeighborToEast()
            {
                var wavefront = Wavefront.New(190, 350, new Vertex(8, 4.5), wavefrontAlgorithm.Vertices.ToList(),
                    10, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];
                wavefront.RemoveNextVertex();
                Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.AreEqual(243.43, angleShadowTo, FLOAT_TOLERANCE);
                Assert.AreEqual(213.69, angleShadowFrom, FLOAT_TOLERANCE);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavefronts[0];
                Assert.AreEqual(wavefront, w);

                w = wavefronts[1];
                Assert.AreEqual(213.69, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(270, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(1, w.RelevantVertices.Count);
                Assert.AreEqual(multiVertexLineObstacle[0].ToPosition(), w.RelevantVertices.First().Position);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void NeighborsOutsideWavefront_NotVisited()
            {
                var wavefront = Wavefront.New(300, 330, new Vertex(8, 2), wavefrontAlgorithm.Vertices.ToList(),
                    10, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];
                Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsFalse(createdWavefront);
                Assert.AreEqual(Double.NaN, angleShadowFrom);
                Assert.AreEqual(Double.NaN, angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var w = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(wavefront, w);
            }

            [Test]
            public void NeighborsInsideWavefront_NotVisited()
            {
                var wavefront = Wavefront.New(270, 360, new Vertex(8, 2), wavefrontAlgorithm.Vertices.ToList(),
                    10, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];
                Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.IsFalse(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsFalse(createdWavefront);
                Assert.AreEqual(Double.NaN, angleShadowFrom);
                Assert.AreEqual(Double.NaN, angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var w = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(wavefront, w);
            }

            [Test]
            public void NeighborsInsideWavefront_BothVisited()
            {
                var wavefront = Wavefront.New(0, 270, new Vertex(6, 4), wavefrontAlgorithm.Vertices.ToList(), 10,
                    false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];
                wavefront.RemoveNextVertex();
                wavefront.RemoveNextVertex();
                Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.IsTrue(wavefront.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavefront.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsFalse(createdWavefront);
                Assert.AreEqual(90, angleShadowFrom, FLOAT_TOLERANCE);
                Assert.AreEqual(180, angleShadowTo, FLOAT_TOLERANCE);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var w = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(wavefront, w);
            }

            [Test]
            public void FirstVertexHasNeighbors_NotCastingShadow()
            {
                var vertices = new List<Vertex>();
                vertices.AddRange(wavefrontAlgorithm.Vertices);
                vertices.Add(new Vertex(5, 2.5));
                var wavefront = Wavefront.New(180, 270, new Vertex(7.5, 3.5), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavefronts[0];
                Assert.AreEqual(wavefront, w);

                w = wavefronts[1];
                Assert.AreEqual(225, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(270, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);
            }

            [Test]
            public void InnerCornerOnLine_NotCreatingNewWavefront()
            {
                var wavefront = Wavefront.New(0, 90, multiVertexLineVertices[0],
                    wavefrontAlgorithm.Vertices.ToList(), 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.False(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(wavefront, ToList(wavefrontAlgorithm.Wavefronts)[0]);
            }

            [Test]
            public void InnerCorner_NotCreatingNewWavefront()
            {
                var wavefront = Wavefront.New(0, 90,
                    new Vertex(6.75, 3.25), wavefrontAlgorithm.Vertices.ToList(),
                    1, false)!;
                wavefrontAlgorithm.AddWavefront(wavefront);
                var vertex = multiVertexLineVertices[1];

                wavefrontAlgorithm.HandleNeighbors(vertex, wavefront, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.False(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(wavefront, ToList(wavefrontAlgorithm.Wavefronts)[0]);
            }
        }

        public class AddNewWavefront : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            [Test]
            public void OneWavefrontAdded()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());

                wavefrontAlgorithm.AddNewWavefront(wavefrontAlgorithm.Vertices.ToList(), rootVertex, 10, 190, 350,
                    false);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var wavefront = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(190, wavefront.FromAngle);
                Assert.AreEqual(350, wavefront.ToAngle);
                Assert.AreEqual(2, wavefront.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavefront.RootVertex);
            }

            [Test]
            public void AngleRangeExceedsZeroDegree()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());

                wavefrontAlgorithm.AddNewWavefront(wavefrontAlgorithm.Vertices.ToList(), rootVertex, 10, 190, 90,
                    false);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);

                var wavefronts = ToList(wavefrontAlgorithm.Wavefronts);
                var wavefront = wavefronts[0];
                Assert.AreEqual(0, wavefront.FromAngle);
                Assert.AreEqual(90, wavefront.ToAngle);
                Assert.AreEqual(3, wavefront.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavefront.RootVertex);

                wavefront = wavefronts[1];
                Assert.AreEqual(190, wavefront.FromAngle);
                Assert.AreEqual(360, wavefront.ToAngle);
                Assert.AreEqual(2, wavefront.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavefront.RootVertex);
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
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(multiVertexLineObstacle);
                obstacleGeometries.Add(rotatedLineObstacle);

                var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void RouteDirectlyToTarget()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 1.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 1.5);

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.Contains(sourceVertex, waypoints);
                Assert.Contains(targetVertex, waypoints);
                Assert.AreEqual(2, waypoints.Count);
            }

            [Test]
            public void RouteOverOneVertex()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 1.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 2.5);

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

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

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

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

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

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

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(sourceVertex, waypoints[0]);
                Assert.AreEqual(rotatedLineObstacle[0].ToPosition(), waypoints[1]);
                Assert.AreEqual(rotatedLineObstacle[1].ToPosition(), waypoints[2]);
                Assert.AreEqual(targetVertex, waypoints[3]);
                Assert.AreEqual(4, waypoints.Count);
            }
        }

        public class ZickZackAroundBuildings
        {
            // Real world problem: Route was not shortest when going around building

            static WavefrontAlgorithm wavefrontAlgorithm;

            [SetUp]
            public void Setup()
            {
                var eastBuilding = new LineString(new[]
                {
                    new Coordinate(1, 2.5),
                    new Coordinate(2.5, 1),
                    new Coordinate(3.5, 2),
                    new Coordinate(2, 3.5),
                    new Coordinate(1, 2.5)
                });
                var westBuilding = new LineString(new[]
                {
                    new Coordinate(3, 3.5),
                    new Coordinate(5, 1.5),
                    new Coordinate(6.5, 3),
                    new Coordinate(4.5, 5),
                    new Coordinate(3, 3.5)
                });
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(eastBuilding);
                obstacleGeometries.Add(westBuilding);

                var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void Routing()
            {
                var routingResult = wavefrontAlgorithm.Route(Position.CreateGeoPosition(2, 1),
                    Position.CreateGeoPosition(3.5, 4.5));
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(5, waypoints.Count);
                Assert.AreEqual(Position.CreateGeoPosition(2, 1), waypoints[0]);
                Assert.AreEqual(Position.CreateGeoPosition(2.5, 1), waypoints[1]);
                Assert.AreEqual(Position.CreateGeoPosition(3.5, 2), waypoints[2]);
                Assert.AreEqual(Position.CreateGeoPosition(3, 3.5), waypoints[3]);
                Assert.AreEqual(Position.CreateGeoPosition(3.5, 4.5), waypoints[4]);
            }
        }

        public class OverlappingObstacles_LineAndPolygon
        {
            // Real world problem: Route was going through barriers

            private WavefrontAlgorithm wavefrontAlgorithm;

            [SetUp]
            public void Setup()
            {
                var obstacle = new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(3, 0),
                    new Coordinate(3, 1),
                    new Coordinate(0, 1),
                    new Coordinate(0, 0)
                });
                var line = new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(0, 1)
                });
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(obstacle);
                obstacleGeometries.Add(line);

                var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void TargetWithinObstacle()
            {
                var source = Position.CreateGeoPosition(1, 2);
                var target = Position.CreateGeoPosition(2, 0.5);
                var routingResult = wavefrontAlgorithm.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(0, waypoints.Count);
            }
        }

        public class OverlappingObstacles_TouchingLines
        {
            // Real world problem: Route was going through the vertex where two lines touched

            private WavefrontAlgorithm wavefrontAlgorithm;
            private LineString line1;
            private LineString line2;
            private LineString line3;

            [SetUp]
            public void Setup()
            {
                line1 = new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(0, 1),
                    new Coordinate(0, 2),
                    new Coordinate(1, 3),
                });
                line2 = new LineString(new[]
                {
                    new Coordinate(0, 1),
                    new Coordinate(1, 1)
                });
                line3 = new LineString(new[]
                {
                    new Coordinate(1, 1),
                    new Coordinate(1, 3)
                });
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(line1);
                obstacleGeometries.Add(line2);
                obstacleGeometries.Add(line3);

                var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void RouteAroundTouchingLines()
            {
                var source = Position.CreateGeoPosition(0.2, 2.25);
                var target = Position.CreateGeoPosition(0.2, 0.5);
                var routingResult = wavefrontAlgorithm.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(4, waypoints.Count);

                Assert.AreEqual(source, waypoints[0]);
                Assert.AreEqual(line1[2].ToPosition(), waypoints[1]);
                Assert.AreEqual(line1[0].ToPosition(), waypoints[2]);
                Assert.AreEqual(target, waypoints[3]);
            }

            [Test]
            public void RouteIntoClosedObstacle()
            {
                var source = Position.CreateGeoPosition(0.2, 2.25);
                var target = Position.CreateGeoPosition(0.2, 1.5);
                var routingResult = wavefrontAlgorithm.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(0, waypoints.Count);
            }
        }

        public class OverlappingObstacles_PolygonSharingEdge
        {
            private List<Obstacle> obstacles;
            private WavefrontAlgorithm wavefrontAlgorithm;
            private Position start;
            private Position target;
            private LineString obstacle1;
            private LineString obstacle2;

            [SetUp]
            public void Setup()
            {
                obstacle1 = new LineString(new[]
                {
                    new Coordinate(1, 20),
                    new Coordinate(1, 3),
                    new Coordinate(4, 3),
                    new Coordinate(4, 4),
                });
                obstacle2 = new LineString(new[]
                {
                    new Coordinate(1, 1),
                    new Coordinate(1, 3),
                    new Coordinate(4, 3),
                    new Coordinate(4, 2),
                });

                obstacles = new List<Obstacle>();
                obstacles.Add(new Obstacle(obstacle1));
                obstacles.Add(new Obstacle(obstacle2));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);

                start = Position.CreateGeoPosition(3, 5);
                target = Position.CreateGeoPosition(0, 3);
            }

            [Test]
            public void ShouldNotRouteAlongTouchingEdge()
            {
                var result = wavefrontAlgorithm.Route(start, target);
                var waypoints = result.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(start, waypoints[0]);
                Assert.AreEqual(obstacle1[3].ToPosition(), waypoints[1]);
                Assert.AreEqual(obstacle2[3].ToPosition(), waypoints[2]);
                Assert.AreEqual(obstacle2[0].ToPosition(), waypoints[3]);
                Assert.AreEqual(target, waypoints[4]);
                Assert.AreEqual(5, waypoints.Count);
            }
        }

        public class TouchingLineEnds
        {
            // Real world problem: Route was going through the vertex where two lines touched

            private WavefrontAlgorithm wavefrontAlgorithm;
            private LineString line1;
            private LineString line2;

            [SetUp]
            public void Setup()
            {
                line1 = new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(0, 1),
                    new Coordinate(3, 3),
                });
                line2 = new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(1, 0)
                });
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(line1);
                obstacleGeometries.Add(line2);

                var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void RouteBelowTouchingLineEnds()
            {
                var source = Position.CreateGeoPosition(0.1, 1.2);
                var target = Position.CreateGeoPosition(0.1, -0.1);
                var routingResult = wavefrontAlgorithm.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(source, waypoints[0]);
                Assert.AreEqual(line1[1].ToPosition(), waypoints[1]);
                Assert.AreEqual(line1[0].ToPosition(), waypoints[2]);
                Assert.AreEqual(target, waypoints[3]);
                Assert.AreEqual(4, waypoints.Count);
            }

            [Test]
            public void RouteAroundTouchingLineEnds()
            {
                var source = Position.CreateGeoPosition(0.1, 1.2);
                var target = Position.CreateGeoPosition(0.1, 0.1);
                var routingResult = wavefrontAlgorithm.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(source, waypoints[0]);
                Assert.AreEqual(line1[1].ToPosition(), waypoints[1]);
                Assert.AreEqual(line1[0].ToPosition(), waypoints[2]);
                Assert.AreEqual(line2[1].ToPosition(), waypoints[3]);
                Assert.AreEqual(target, waypoints[4]);
                Assert.AreEqual(5, waypoints.Count);
            }
        }

        /// <summary>
        /// Beware: This removes everything from the heap as one cannot iterate over the heap.
        /// </summary>
        public static List<Wavefront> ToList(FibonacciHeap<Wavefront, double> wavefronts)
        {
            var list = new List<Wavefront>();
            while (!wavefronts.IsEmpty())
            {
                list.Add(wavefronts.Min().Data);
                wavefronts.RemoveMin();
            }

            return list;
        }
    }
}