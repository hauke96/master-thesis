using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                var wavelet = Wavelet.New(0, 90, new Vertex(1, 1), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                // Remove last remaining vertex
                wavelet.RemoveNextVertex();

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
                var wavelet = Wavelet.New(0, 90, new Vertex(6.5, 2.9), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(1, wavelet.RelevantVertices.Count);
            }

            [Test]
            public void EventVertexHasAlreadyBeenProcessed()
            {
                var vertices = new List<Vertex>();
                var nextVertex = multiVertexLineVertices[0];
                vertices.Add(nextVertex);
                vertices.Add(multiVertexLineVertices[1]);
                var wavelet = Wavelet.New(0, 90, new Vertex(5, 2), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                wavefrontAlgorithm.WaypointToPredecessor[new Waypoint(nextVertex.Position, 0, 0)] =
                    new Waypoint(Position.CreateGeoPosition(1, 1), 0, 0);
                wavefrontAlgorithm.WavefrontRootPredecessor.Add(new Waypoint(nextVertex.Position, 0, 0), null);
                wavefrontAlgorithm.WavefrontRootToWaypoint.Add(nextVertex.Position,
                    wavefrontAlgorithm.WavefrontRootPredecessor.First().Key);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(1, wavelet.RelevantVertices.Count);
            }

            [Test]
            public void FirstVertexHasNeighbors_NotCastingShadow()
            {
                var vertices = new List<Vertex>();
                vertices.AddRange(wavefrontAlgorithm.Vertices);
                vertices.Add(new Vertex(5, 2.5));
                var wavelet = Wavelet.New(180, 270, new Vertex(7.5, 3.5), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                Assert.AreEqual(multiVertexLineVertices[1].Position, wavelet.GetNextVertex()?.Position);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);

                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavelets[0];
                Assert.AreEqual(wavelet, w);

                w = wavelets[1];
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

                var wavelet = Wavelet.New(0, 90, sourceVertex, vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);

                var rootWaypoint = new Waypoint(wavelet.RootVertex.Position, 0, 0);
                wavefrontAlgorithm.WaypointToPredecessor[rootWaypoint] = null;
                wavefrontAlgorithm.PositionToWaypoint[rootWaypoint.Position] = rootWaypoint;

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var targetWaypoint =
                    wavefrontAlgorithm.WaypointToPredecessor.Keys.First(k => k.Position.Equals(targetPosition));
                Assert.NotNull(wavefrontAlgorithm.WaypointToPredecessor[targetWaypoint]);
                Assert.AreEqual(wavelet.RootVertex.Position,
                    wavefrontAlgorithm.WaypointToPredecessor[targetWaypoint].Position);
                Assert.AreEqual(0, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(0, wavelet.RelevantVertices.Count);
            }

            [Test]
            public void WaveletFromAndToWithinShadowArea()
            {
                var wavelet = Wavelet.New(10, 350, new Vertex(6.5, 2.5), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());
                wavefrontAlgorithm.AddWavefront(wavelet);

                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.AreEqual(2, wavelets.Count);
                Assert.AreEqual(0, wavelets[0].FromAngle);
                Assert.AreEqual(45, wavelets[0].ToAngle);
                Assert.AreEqual(45, wavelets[1].FromAngle);
                Assert.AreEqual(315, wavelets[1].ToAngle);
            }

            [Test]
            public void WaveletFromWithinShadowArea()
            {
                wavefrontAlgorithm.Vertices.Add(new Vertex(10, 3));
                var wavelet = Wavelet.New(0.0001, 90, new Vertex(6, 2), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;

                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(multiVertexLineVertices[1], wavelet.GetNextVertex());

                wavefrontAlgorithm.AddWavefront(wavelet);
                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.AreEqual(2, wavelets.Count);
                Assert.AreEqual(0, wavelets[0].FromAngle);
                Assert.AreEqual(45, wavelets[0].ToAngle);
                Assert.AreEqual(multiVertexLineVertices[1], wavelets[0].RootVertex);
                Assert.AreEqual(45, wavelets[1].FromAngle);
                Assert.AreEqual(90, wavelets[1].ToAngle);
                Assert.AreEqual(wavelet.RootVertex, wavelets[1].RootVertex);
            }

            [Test]
            public void WaveletToWithinShadowArea()
            {
                wavefrontAlgorithm.Vertices.Add(new Vertex(7, 2.8));
                var wavelet = Wavelet.New(180, 269.9999, new Vertex(8, 4), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;

                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(multiVertexLineVertices[1], wavelet.GetNextVertex());

                wavefrontAlgorithm.AddWavefront(wavelet);
                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.AreEqual(2, wavelets.Count);
                Assert.AreEqual(180, wavelets[0].FromAngle);
                Assert.AreEqual(225, wavelets[0].ToAngle);
                Assert.AreEqual(wavelet.RootVertex, wavelets[0].RootVertex);
                Assert.AreEqual(225, wavelets[1].FromAngle);
                Assert.AreEqual(270, wavelets[1].ToAngle);
                Assert.AreEqual(multiVertexLineVertices[1], wavelets[1].RootVertex);
            }
        }

        public class WithMultiVertexLineFullyInsideWavefront : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            Wavelet _wavelet;
            Vertex nextVertex;
            Position targetPosition;

            [SetUp]
            public void Setup()
            {
                nextVertex = multiVertexLineVertices[0];
                _wavelet = Wavelet.New(0, 350, new Vertex(5, 0), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;
                wavefrontAlgorithm.AddWavefront(_wavelet);
                targetPosition = Position.CreateGeoPosition(10, 10);

                var rootWaypoint = new Waypoint(_wavelet.RootVertex.Position, 0, 0);
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
                Assert.AreEqual(_wavelet.RootVertex.Position,
                    wavefrontAlgorithm.WaypointToPredecessor[waypoint].Position);
            }

            [Test]
            public void RemovesNextVertexFromWavefront()
            {
                Assert.IsFalse(_wavelet.RelevantVertices.Contains(nextVertex));
            }

            [Test]
            public void NotCastingShadow_NotRemovingOldWavefront()
            {
                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.IsTrue(wavelets.Contains(_wavelet));
                Assert.AreEqual(2, wavelets.Count);

                var w = wavelets[0];
                Assert.AreEqual(_wavelet, w);

                w = wavelets[1];
                Assert.AreEqual(18.43, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle);
                Assert.AreEqual(multiVertexLineObstacle[0], w.RootVertex.Coordinate);
            }

            [Test]
            public void NextVertexCastingShadow()
            {
                wavefrontAlgorithm.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.IsFalse(wavelets.Contains(_wavelet));
                Assert.AreEqual(3, wavelets.Count);

                var w = wavelets[0];
                Assert.AreEqual(18.43, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle);
                Assert.AreEqual(multiVertexLineObstacle[0], w.RootVertex.Coordinate);

                w = wavelets[1];
                Assert.AreEqual(0, w.FromAngle);
                Assert.AreEqual(33.69, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);

                w = wavelets[2];
                Assert.AreEqual(33.69, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(350, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(_wavelet.RootVertex, w.RootVertex);
            }
        }

        public class NewWavefrontExceedingZeroDegree : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            Wavelet _wavelet;
            Vertex nextVertex;
            Position targetPosition;

            [SetUp]
            public void Setup()
            {
                nextVertex = multiVertexLineVertices[0];
                // Add wavelet close to the next vertex
                _wavelet = Wavelet.New(270, 355, new Vertex(6.2, 2.8), wavefrontAlgorithm.Vertices.ToList(), 1,
                    false)!;
                wavefrontAlgorithm.AddWavefront(_wavelet);
                targetPosition = Position.CreateGeoPosition(10, 10);

                var rootWaypoint = new Waypoint(_wavelet.RootVertex.Position, 0, 0);
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
                Assert.AreEqual(_wavelet.RootVertex.Position,
                    wavefrontAlgorithm.WaypointToPredecessor[waypoint].Position);
            }

            [Test]
            public void RemovesNextVertexFromWavefront()
            {
                Assert.IsFalse(_wavelet.RelevantVertices.Contains(nextVertex));
            }

            [Test]
            public void ReplacesOriginalWavefront()
            {
                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.Contains(_wavelet, wavelets);
                Assert.AreEqual(3, wavelets.Count);

                var w = wavelets[0];
                Assert.AreEqual(0, w.FromAngle);
                Assert.AreEqual(90, w.ToAngle, FLOAT_TOLERANCE);

                w = wavelets[1];
                Assert.AreEqual(270, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(355, w.ToAngle, FLOAT_TOLERANCE);

                w = wavelets[2];
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
                wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices.ToList(), rootVertex, 10,
                    10, 10, false);
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());
            }

            [Test]
            public void NewWavefrontWouldBeInvalid()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());
                wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices.ToList(), rootVertex, 10,
                    10, 11, false);
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());
            }

            [Test]
            public void NewWavefrontAdded()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());

                wavefrontAlgorithm.AddWavefrontIfValid(wavefrontAlgorithm.Vertices.ToList(), rootVertex, 10,
                    190, 360, false);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var wavelet = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(190, wavelet.FromAngle);
                Assert.AreEqual(360, wavelet.ToAngle);
                Assert.AreEqual(2, wavelet.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavelet.RootVertex);
            }
        }

        public class HandleNeighbors : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            [Test]
            public void NoNeighborToWest()
            {
                var wavelet = Wavelet.New(0, 90, new Vertex(5, 0), wavefrontAlgorithm.Vertices.ToList(), 10,
                    false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.AreEqual(18.435, angleShadowFrom, FLOAT_TOLERANCE);
                Assert.AreEqual(33.69, angleShadowTo, FLOAT_TOLERANCE);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavelets[0];
                Assert.AreEqual(wavelet, w);

                w = wavelets[1];
                Assert.AreEqual(0, w.FromAngle);
                Assert.AreEqual(33.69, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(1, w.RelevantVertices.Count);
                Assert.AreEqual(multiVertexLineObstacle[2].ToPosition(), w.RelevantVertices.First().Position);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void EndOfLine_CreatesNew180DegreeWavefront()
            {
                var wavelet = Wavelet.New(180, 270, new Vertex(multiVertexLineObstacle[1].ToPosition()),
                    wavefrontAlgorithm.Vertices.ToList(), 10, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[0];
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                Assert.AreEqual(3, wavelets.Count);
                var w = wavelets[0];
                Assert.AreEqual(wavelet, w);

                w = wavelets[1];
                Assert.AreEqual(0, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(2, w.RelevantVertices.Count);
                Assert.AreEqual(vertex, w.RootVertex);

                w = wavelets[2];
                Assert.AreEqual(270, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(360, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(2, w.RelevantVertices.Count);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void StartingFromEndOfLine_CreatesNewWavefront()
            {
                var wavelet = Wavelet.New(90, 180, new Vertex(multiVertexLineObstacle[0].ToPosition()),
                    wavefrontAlgorithm.Vertices.ToList(), 10, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavelets[0];
                Assert.AreEqual(wavelet, w);

                w = wavelets[1];
                Assert.AreEqual(0, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(90, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(1, w.RelevantVertices.Count);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void NoNeighborToEast()
            {
                var wavelet = Wavelet.New(190, 350, new Vertex(8, 4.5), wavefrontAlgorithm.Vertices.ToList(),
                    10, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.AreEqual(243.43, angleShadowTo, FLOAT_TOLERANCE);
                Assert.AreEqual(213.69, angleShadowFrom, FLOAT_TOLERANCE);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavelets[0];
                Assert.AreEqual(wavelet, w);

                w = wavelets[1];
                Assert.AreEqual(213.69, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(270, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(1, w.RelevantVertices.Count);
                Assert.AreEqual(multiVertexLineObstacle[0].ToPosition(), w.RelevantVertices.First().Position);
                Assert.AreEqual(vertex, w.RootVertex);
            }

            [Test]
            public void NeighborsOutsideWavefront_NotVisited()
            {
                var wavelet = Wavelet.New(300, 330, new Vertex(8, 2), wavefrontAlgorithm.Vertices.ToList(),
                    10, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];
                Assert.IsFalse(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsFalse(createdWavefront);
                Assert.AreEqual(Double.NaN, angleShadowFrom);
                Assert.AreEqual(Double.NaN, angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var w = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(wavelet, w);
            }

            [Test]
            public void NeighborsInsideWavefront_NotVisited()
            {
                var wavelet = Wavelet.New(270, 360, new Vertex(8, 2), wavefrontAlgorithm.Vertices.ToList(),
                    10, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];
                Assert.IsFalse(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.IsFalse(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsFalse(createdWavefront);
                Assert.AreEqual(Double.NaN, angleShadowFrom);
                Assert.AreEqual(Double.NaN, angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var w = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(wavelet, w);
            }

            [Test]
            public void NeighborsInsideWavefront_BothVisited()
            {
                var wavelet = Wavelet.New(0, 270, new Vertex(6, 4), wavefrontAlgorithm.Vertices.ToList(), 10,
                    false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsFalse(createdWavefront);
                Assert.AreEqual(90, angleShadowFrom, FLOAT_TOLERANCE);
                Assert.AreEqual(180, angleShadowTo, FLOAT_TOLERANCE);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                var w = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(wavelet, w);
            }

            [Test]
            public void FirstVertexHasNeighbors_NotCastingShadow()
            {
                var vertices = new List<Vertex>();
                vertices.AddRange(wavefrontAlgorithm.Vertices);
                vertices.Add(new Vertex(5, 2.5));
                var wavelet = Wavelet.New(180, 270, new Vertex(7.5, 3.5), vertices, 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsTrue(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);
                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                var w = wavelets[0];
                Assert.AreEqual(wavelet, w);

                w = wavelets[1];
                Assert.AreEqual(225, w.FromAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(270, w.ToAngle, FLOAT_TOLERANCE);
                Assert.AreEqual(multiVertexLineObstacle[1], w.RootVertex.Coordinate);
            }

            [Test]
            public void InnerCornerOnLine_NotCreatingNewWavefront()
            {
                var wavelet = Wavelet.New(0, 90, multiVertexLineVertices[0],
                    wavefrontAlgorithm.Vertices.ToList(), 1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.False(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(wavelet, ToList(wavefrontAlgorithm.Wavefronts)[0]);
            }

            [Test]
            public void InnerCorner_NotCreatingNewWavefront()
            {
                var wavelet = Wavelet.New(0, 90,
                    new Vertex(6.75, 3.25), wavefrontAlgorithm.Vertices.ToList(),
                    1, false)!;
                wavefrontAlgorithm.AddWavefront(wavelet);
                var vertex = multiVertexLineVertices[1];

                wavefrontAlgorithm.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.False(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(1, wavefrontAlgorithm.Wavefronts.Count);
                Assert.AreEqual(wavelet, ToList(wavefrontAlgorithm.Wavefronts)[0]);
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
                var wavelet = ToList(wavefrontAlgorithm.Wavefronts)[0];
                Assert.AreEqual(190, wavelet.FromAngle);
                Assert.AreEqual(350, wavelet.ToAngle);
                Assert.AreEqual(2, wavelet.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavelet.RootVertex);
            }

            [Test]
            public void AngleRangeExceedsZeroDegree()
            {
                Assert.True(wavefrontAlgorithm.Wavefronts.IsEmpty());

                wavefrontAlgorithm.AddNewWavefront(wavefrontAlgorithm.Vertices.ToList(), rootVertex, 10, 190, 90,
                    false);

                Assert.AreEqual(2, wavefrontAlgorithm.Wavefronts.Count);

                var wavelets = ToList(wavefrontAlgorithm.Wavefronts);
                var wavelet = wavelets[0];
                Assert.AreEqual(0, wavelet.FromAngle);
                Assert.AreEqual(90, wavelet.ToAngle);
                Assert.AreEqual(3, wavelet.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavelet.RootVertex);

                wavelet = wavelets[1];
                Assert.AreEqual(190, wavelet.FromAngle);
                Assert.AreEqual(360, wavelet.ToAngle);
                Assert.AreEqual(2, wavelet.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavelet.RootVertex);
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

        public class RouteWithAlignedObstacles
        {
            static WavefrontAlgorithm wavefrontAlgorithm;

            [SetUp]
            public void Setup()
            {
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(2, 1),
                    new Coordinate(2, 2),
                    new Coordinate(1, 2),
                    new Coordinate(1, 1),
                    new Coordinate(2, 1)
                }));
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(3, 1),
                    new Coordinate(3, 2),
                    new Coordinate(4, 2)
                }));
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(5, 2),
                    new Coordinate(5, 1),
                    new Coordinate(5.5, 1),
                    new Coordinate(6, 1),
                    new Coordinate(6, 2)
                }));
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(2, 0.5),
                    new Coordinate(2, 1)
                }));
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(5.5, 0.5),
                    new Coordinate(5.5, 1)
                }));
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(3, 0.5),
                    new Coordinate(3, 1)
                }));

                var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void RouteAlongTopAlignedObstacles()
            {
                var sourceVertex = Position.CreateGeoPosition(0, 1.9);
                var targetVertex = Position.CreateGeoPosition(7, 1.9);

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var routeCoordinates = routingResult.OptimalRoute.Map(w => w.Position.ToCoordinate());

                var expectedRoute = new List<Coordinate>()
                {
                    sourceVertex.ToCoordinate(),
                    new Coordinate(1, 2),
                    new Coordinate(6, 2),
                    targetVertex.ToCoordinate()
                };

                CollectionAssert.AreEqual(expectedRoute, routeCoordinates);
            }

            [Test]
            public void RouteAlongBottomAlignedObstacles()
            {
                var sourceVertex = Position.CreateGeoPosition(0.9, 1.1);
                var targetVertex = Position.CreateGeoPosition(6.1, 1.1);

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var routeCoordinates = routingResult.OptimalRoute.Map(w => w.Position.ToCoordinate());

                var expectedRoute = new List<Coordinate>()
                {
                    sourceVertex.ToCoordinate(),
                    new Coordinate(1, 1),
                    new Coordinate(2, 0.5),
                    new Coordinate(5.5, 0.5),
                    new Coordinate(6, 1),
                    targetVertex.ToCoordinate()
                };

                CollectionAssert.AreEqual(expectedRoute, routeCoordinates);
            }

            [Test]
            public void RouteAroundTouchingLines()
            {
                // Make sure the target (at height 1) is not visible from the source (at height 1) through the touching
                // lines (both touching at height 1).

                var sourceVertex = Position.CreateGeoPosition(2.5, 1);
                var targetVertex = Position.CreateGeoPosition(3.5, 1);

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var routeCoordinates = routingResult.OptimalRoute.Map(w => w.Position.ToCoordinate());

                var expectedRoute = new List<Coordinate>()
                {
                    sourceVertex.ToCoordinate(),
                    new Coordinate(3, 0.5),
                    targetVertex.ToCoordinate()
                };

                CollectionAssert.AreEqual(expectedRoute, routeCoordinates);
            }
        }

        public class RouteIntoOpenObstacle
        {
            static WavefrontAlgorithm wavefrontAlgorithm;

            [SetUp]
            public void Setup()
            {
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(1, 1),
                    new Coordinate(1, 0),
                    new Coordinate(0, 0),
                    new Coordinate(1, 3),
                    new Coordinate(1, 2)
                }));

                var obstacles = obstacleGeometries.Map(geometry => new Obstacle(geometry));

                wavefrontAlgorithm = new WavefrontAlgorithm(obstacles);
            }

            [Test]
            public void RouteIntoObstacle()
            {
                var sourceVertex = Position.CreateGeoPosition(0.8, -0.8);
                var targetVertex = Position.CreateGeoPosition(0.8, 0.5);

                var routingResult = wavefrontAlgorithm.Route(sourceVertex, targetVertex);
                var routeCoordinates = routingResult.OptimalRoute.Map(w => w.Position.ToCoordinate());

                var expectedRoute = new List<Coordinate>()
                {
                    sourceVertex.ToCoordinate(),
                    new Coordinate(1, 0),
                    new Coordinate(1, 1),
                    targetVertex.ToCoordinate()
                };

                CollectionAssert.AreEqual(expectedRoute, routeCoordinates);
            }
        }

        public class RouteWithoutObstacles
        {
            static WavefrontAlgorithm wavefrontAlgorithm;

            [SetUp]
            public void Setup()
            {
                wavefrontAlgorithm = new WavefrontAlgorithm(new List<Obstacle>());
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
        public static List<Wavelet> ToList(FibonacciHeap<Wavelet, double> wavelets)
        {
            var list = new List<Wavelet>();
            while (!wavelets.IsEmpty())
            {
                list.Add(wavelets.Min().Data);
                wavelets.RemoveMin();
            }

            return list;
        }
    }
}