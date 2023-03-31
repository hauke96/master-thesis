using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mars.Common;
using Mars.Common.Collections;
using NetTopologySuite.Geometries;
using NetTopologySuite.Features;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;
using Feature = NetTopologySuite.Features.Feature;
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
                HybridGeometricRouter.AddWavelet(wavelet);
                // Remove last remaining vertex
                wavelet.RemoveNextVertex();

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(0, HybridGeometricRouter.Wavelets.Count);
            }

            [Test]
            public void EventIsNotValid()
            {
                var vertices = new List<Vertex>();
                vertices.Add(new Vertex(6.5, 3.1));
                vertices.Add(multiVertexLineVertices[1]);
                // Between but slightly below the multi-vertex-line
                var wavelet = Wavelet.New(0, 90, new Vertex(6.5, 2.9), vertices, 1, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
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
                HybridGeometricRouter.AddWavelet(wavelet);
                HybridGeometricRouter.WaypointToPredecessor[new Waypoint(nextVertex.Position, 0, 0, 0)] =
                    new Waypoint(Position.CreateGeoPosition(1, 1), 0, 0, 0);
                HybridGeometricRouter.WaveletRootPredecessor.Add(new Waypoint(nextVertex.Position, 0, 0, 0), null);
                HybridGeometricRouter.WaveletRootToWaypoint.Add(nextVertex.Position,
                    HybridGeometricRouter.WaveletRootPredecessor.First().Key);

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                Assert.AreEqual(1, wavelet.RelevantVertices.Count);
            }

            [Test]
            public void FirstVertexHasNeighbors_NotCastingShadow()
            {
                var vertices = new List<Vertex>();
                vertices.AddRange(HybridGeometricRouter.Vertices);
                vertices.Add(new Vertex(5, 2.5));
                var wavelet = Wavelet.New(180, 270, new Vertex(7.5, 3.5), vertices, 1, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                Assert.AreEqual(multiVertexLineVertices[1].Position, wavelet.GetNextVertex()?.Position);

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                Assert.AreEqual(2, HybridGeometricRouter.Wavelets.Count);

                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                HybridGeometricRouter.AddWavelet(wavelet);

                var rootWaypoint = new Waypoint(wavelet.RootVertex.Position, 0, 0, 0);
                HybridGeometricRouter.WaypointToPredecessor[rootWaypoint] = null;
                HybridGeometricRouter.PositionToWaypoint[rootWaypoint.Position] = rootWaypoint;

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                var targetWaypoint =
                    HybridGeometricRouter.WaypointToPredecessor.Keys.First(k => k.Position.Equals(targetPosition));
                Assert.NotNull(HybridGeometricRouter.WaypointToPredecessor[targetWaypoint]);
                Assert.AreEqual(wavelet.RootVertex.Position,
                    HybridGeometricRouter.WaypointToPredecessor[targetWaypoint].Position);
                Assert.AreEqual(0, HybridGeometricRouter.Wavelets.Count);
                Assert.AreEqual(0, wavelet.RelevantVertices.Count);
            }

            [Test]
            public void WaveletFromAndToWithinShadowArea()
            {
                var wavelet = Wavelet.New(10, 350, new Vertex(6.5, 2.5), HybridGeometricRouter.Vertices.ToList(), 1,
                    false)!;
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());
                HybridGeometricRouter.AddWavelet(wavelet);

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(HybridGeometricRouter.Wavelets);
                Assert.AreEqual(2, wavelets.Count);
                Assert.AreEqual(0, wavelets[0].FromAngle);
                Assert.AreEqual(45, wavelets[0].ToAngle);
                Assert.AreEqual(45, wavelets[1].FromAngle);
                Assert.AreEqual(315, wavelets[1].ToAngle);
            }

            [Test]
            public void WaveletFromWithinShadowArea()
            {
                HybridGeometricRouter.Vertices.Add(new Vertex(10, 3));
                var wavelet = Wavelet.New(0.0001, 90, new Vertex(6, 2), HybridGeometricRouter.Vertices.ToList(), 1,
                    false)!;

                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(multiVertexLineVertices[1], wavelet.GetNextVertex());

                HybridGeometricRouter.AddWavelet(wavelet);
                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                HybridGeometricRouter.Vertices.Add(new Vertex(7, 2.8));
                var wavelet = Wavelet.New(180, 269.9999, new Vertex(8, 4), HybridGeometricRouter.Vertices.ToList(), 1,
                    false)!;

                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(multiVertexLineVertices[1], wavelet.GetNextVertex());

                HybridGeometricRouter.AddWavelet(wavelet);
                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(HybridGeometricRouter.Wavelets);
                Assert.AreEqual(2, wavelets.Count);
                Assert.AreEqual(180, wavelets[0].FromAngle);
                Assert.AreEqual(225, wavelets[0].ToAngle);
                Assert.AreEqual(wavelet.RootVertex, wavelets[0].RootVertex);
                Assert.AreEqual(225, wavelets[1].FromAngle);
                Assert.AreEqual(270, wavelets[1].ToAngle);
                Assert.AreEqual(multiVertexLineVertices[1], wavelets[1].RootVertex);
            }
        }

        public class WithCollinearObstacle
        {
            List<Vertex> simpleLineVertices;
            HybridGeometricRouter _hybridGeometricRouter;

            [SetUp]
            public void setup()
            {
                var c0 = new Coordinate(1, 1);
                var c1 = new Coordinate(2, 1);
                var c2 = new Coordinate(3, 1);
                var c3 = new Coordinate(4, 1);

                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(new LineString(new[] { c0, c1 }));
                obstacleGeometries.Add(new LineString(new[] { c1, c2 }));
                obstacleGeometries.Add(new LineString(new[] { c2, c3 }));
                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                simpleLineVertices = new List<Vertex>();
                simpleLineVertices.Add(new Vertex(c0.ToPosition(), c1.ToPosition()));
                simpleLineVertices.Add(new Vertex(c1.ToPosition(), c0.ToPosition(), c2.ToPosition()));
                simpleLineVertices.Add(new Vertex(c2.ToPosition(), c1.ToPosition(), c3.ToPosition()));
                simpleLineVertices.Add(new Vertex(c3.ToPosition(), c2.ToPosition()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void CollinearSegmentsCauseNewWavelet()
            {
                var relevantVertices = new List<Vertex>();
                relevantVertices.Add(simpleLineVertices[1]);
                var wavelet = Wavelet.New(0, 90, simpleLineVertices[0], relevantVertices, 1, false)!;
                _hybridGeometricRouter.AddWavelet(wavelet);

                _hybridGeometricRouter.ProcessNextEvent(new Position(5, 1), new Stopwatch());

                Assert.AreEqual(0, wavelet.RelevantVertices.Count);
                var wavelets = ToList(_hybridGeometricRouter.Wavelets);
                Assert.IsFalse(wavelets.Contains(wavelet));

                Assert.AreEqual(1, wavelets.Count);
                var newWavelet = wavelets[0];
                Assert.AreEqual(new List<Vertex> { simpleLineVertices[2] }, newWavelet.RelevantVertices);
                Assert.AreEqual(90, newWavelet.FromAngle);
                Assert.AreEqual(90, newWavelet.ToAngle);
            }

            [Test]
            public void CollinearSegments_EndProduces180DegreeWavelet()
            {
                var relevantVertices = new List<Vertex>();
                relevantVertices.Add(simpleLineVertices[3]);
                var wavelet = Wavelet.New(0, 90, simpleLineVertices[2], relevantVertices, 1, false)!;
                _hybridGeometricRouter.AddWavelet(wavelet);

                _hybridGeometricRouter.ProcessNextEvent(new Position(5, 1), new Stopwatch());

                Assert.AreEqual(0, wavelet.RelevantVertices.Count);
                var wavelets = ToList(_hybridGeometricRouter.Wavelets);
                Assert.IsFalse(wavelets.Contains(wavelet));

                Assert.AreEqual(1, wavelets.Count);
                var newWavelet = wavelets[0];
                Assert.AreEqual(simpleLineVertices[3], newWavelet.RootVertex);
                CollectionAssert.AreEquivalent(new List<Vertex> { simpleLineVertices[1], simpleLineVertices[2] },
                    newWavelet.RelevantVertices);
                Assert.AreEqual(90, newWavelet.FromAngle);
                Assert.AreEqual(270, newWavelet.ToAngle);
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
                _wavelet = Wavelet.New(0, 350, new Vertex(5, 0), HybridGeometricRouter.Vertices.ToList(), 1,
                    false)!;
                HybridGeometricRouter.AddWavelet(_wavelet);
                targetPosition = Position.CreateGeoPosition(10, 10);

                var rootWaypoint = new Waypoint(_wavelet.RootVertex.Position, 0, 0, 0);
                HybridGeometricRouter.WaypointToPredecessor[rootWaypoint] = null;
                HybridGeometricRouter.PositionToWaypoint[rootWaypoint.Position] = rootWaypoint;

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());
            }

            [Test]
            public void SetsPredecessorCorrectly()
            {
                var waypoint =
                    HybridGeometricRouter.WaypointToPredecessor.Keys.First(k => k.Position.Equals(nextVertex.Position));
                Assert.NotNull(HybridGeometricRouter.WaypointToPredecessor[waypoint]);
                Assert.AreEqual(_wavelet.RootVertex.Position,
                    HybridGeometricRouter.WaypointToPredecessor[waypoint].Position);
            }

            [Test]
            public void RemovesNextVertexFromWavefront()
            {
                Assert.IsFalse(_wavelet.RelevantVertices.Contains(nextVertex));
            }

            [Test]
            public void NotCastingShadow_NotRemovingOldWavefront()
            {
                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());

                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                _wavelet = Wavelet.New(270, 355, new Vertex(6.2, 2.8), HybridGeometricRouter.Vertices.ToList(), 1,
                    false)!;
                HybridGeometricRouter.AddWavelet(_wavelet);
                targetPosition = Position.CreateGeoPosition(10, 10);

                var rootWaypoint = new Waypoint(_wavelet.RootVertex.Position, 0, 0, 0);
                HybridGeometricRouter.WaypointToPredecessor[rootWaypoint] = null;
                HybridGeometricRouter.PositionToWaypoint[rootWaypoint.Position] = rootWaypoint;

                HybridGeometricRouter.ProcessNextEvent(targetPosition, new Stopwatch());
            }

            [Test]
            public void SetsPredecessorCorrectly()
            {
                var waypoint =
                    HybridGeometricRouter.WaypointToPredecessor.Keys.First(k => k.Position.Equals(nextVertex.Position));
                Assert.NotNull(HybridGeometricRouter.WaypointToPredecessor[waypoint]);
                Assert.AreEqual(_wavelet.RootVertex.Position,
                    HybridGeometricRouter.WaypointToPredecessor[waypoint].Position);
            }

            [Test]
            public void RemovesNextVertexFromWavefront()
            {
                Assert.IsFalse(_wavelet.RelevantVertices.Contains(nextVertex));
            }

            [Test]
            public void ReplacesOriginalWavefront()
            {
                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                Assert.True(HybridGeometricRouter.Wavelets.IsEmpty());
                HybridGeometricRouter.AddWaveletIfValid(HybridGeometricRouter.Vertices.ToList(), rootVertex, 10,
                    10, 10, false);
                Assert.True(HybridGeometricRouter.Wavelets.IsEmpty());
            }

            [Test]
            public void NewWavefrontWouldBeInvalid()
            {
                Assert.True(HybridGeometricRouter.Wavelets.IsEmpty());
                HybridGeometricRouter.AddWaveletIfValid(HybridGeometricRouter.Vertices.ToList(), rootVertex, 10,
                    10, 11, false);
                Assert.True(HybridGeometricRouter.Wavelets.IsEmpty());
            }

            [Test]
            public void NewWavefrontAdded()
            {
                Assert.True(HybridGeometricRouter.Wavelets.IsEmpty());

                HybridGeometricRouter.AddWaveletIfValid(HybridGeometricRouter.Vertices.ToList(), rootVertex, 10,
                    190, 360, false);

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                var wavelet = ToList(HybridGeometricRouter.Wavelets)[0];
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
                var wavelet = Wavelet.New(0, 90, new Vertex(5, 0), HybridGeometricRouter.Vertices.ToList(), 10,
                    false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsNotEmpty(createdWavefront);
                Assert.AreEqual(18.435, angleShadowFrom, FLOAT_TOLERANCE);
                Assert.AreEqual(33.69, angleShadowTo, FLOAT_TOLERANCE);

                Assert.AreEqual(2, HybridGeometricRouter.Wavelets.Count);
                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                    HybridGeometricRouter.Vertices.ToList(), 10, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[0];
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsNotEmpty(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                    HybridGeometricRouter.Vertices.ToList(), 10, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsNotEmpty(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(2, HybridGeometricRouter.Wavelets.Count);
                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                var wavelet = Wavelet.New(190, 350, new Vertex(8, 4.5), HybridGeometricRouter.Vertices.ToList(),
                    10, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsNotEmpty(createdWavefront);
                Assert.AreEqual(243.43, angleShadowTo, FLOAT_TOLERANCE);
                Assert.AreEqual(213.69, angleShadowFrom, FLOAT_TOLERANCE);

                Assert.AreEqual(2, HybridGeometricRouter.Wavelets.Count);
                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                var wavelet = Wavelet.New(300, 330, new Vertex(8, 2), HybridGeometricRouter.Vertices.ToList(),
                    10, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];
                Assert.IsFalse(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsEmpty(createdWavefront);
                Assert.AreEqual(Double.NaN, angleShadowFrom);
                Assert.AreEqual(Double.NaN, angleShadowTo);

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                var w = ToList(HybridGeometricRouter.Wavelets)[0];
                Assert.AreEqual(wavelet, w);
            }

            [Test]
            public void NeighborsInsideWavefront_NotVisited()
            {
                var wavelet = Wavelet.New(270, 360, new Vertex(8, 2), HybridGeometricRouter.Vertices.ToList(),
                    10, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];
                Assert.IsFalse(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.IsFalse(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsEmpty(createdWavefront);
                Assert.AreEqual(Double.NaN, angleShadowFrom);
                Assert.AreEqual(Double.NaN, angleShadowTo);

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                var w = ToList(HybridGeometricRouter.Wavelets)[0];
                Assert.AreEqual(wavelet, w);
            }

            [Test]
            public void NeighborsInsideWavefront_BothVisited()
            {
                var wavelet = Wavelet.New(0, 270, new Vertex(6, 4), HybridGeometricRouter.Vertices.ToList(), 10,
                    false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];
                wavelet.RemoveNextVertex();
                wavelet.RemoveNextVertex();
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[0].ToPosition()));
                Assert.IsTrue(wavelet.HasBeenVisited(multiVertexLineObstacle[2].ToPosition()));
                Assert.AreEqual(vertex, wavelet.GetNextVertex());

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsEmpty(createdWavefront);
                Assert.AreEqual(90, angleShadowFrom, FLOAT_TOLERANCE);
                Assert.AreEqual(180, angleShadowTo, FLOAT_TOLERANCE);

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                var w = ToList(HybridGeometricRouter.Wavelets)[0];
                Assert.AreEqual(wavelet, w);
            }

            [Test]
            public void FirstVertexHasNeighbors_NotCastingShadow()
            {
                var vertices = new List<Vertex>();
                vertices.AddRange(HybridGeometricRouter.Vertices);
                vertices.Add(new Vertex(5, 2.5));
                var wavelet = Wavelet.New(180, 270, new Vertex(7.5, 3.5), vertices, 1, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsNotEmpty(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(2, HybridGeometricRouter.Wavelets.Count);
                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
                    HybridGeometricRouter.Vertices.ToList(), 1, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsEmpty(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                Assert.AreEqual(wavelet, ToList(HybridGeometricRouter.Wavelets)[0]);
            }

            [Test]
            public void InnerCorner_NotCreatingNewWavefront()
            {
                var wavelet = Wavelet.New(0, 90,
                    new Vertex(6.75, 3.25), HybridGeometricRouter.Vertices.ToList(),
                    1, false)!;
                HybridGeometricRouter.AddWavelet(wavelet);
                var vertex = multiVertexLineVertices[1];

                HybridGeometricRouter.HandleNeighbors(vertex, wavelet, out var angleShadowFrom,
                    out var angleShadowTo, out var createdWavefront);

                Assert.IsEmpty(createdWavefront);
                Assert.IsNaN(angleShadowFrom);
                Assert.IsNaN(angleShadowTo);

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                Assert.AreEqual(wavelet, ToList(HybridGeometricRouter.Wavelets)[0]);
            }
        }

        public class AddNewWavefront : WavefrontTestHelper.WithWavefrontAlgorithm
        {
            [Test]
            public void OneWavefrontAdded()
            {
                Assert.True(HybridGeometricRouter.Wavelets.IsEmpty());

                HybridGeometricRouter.AddNewWavelet(HybridGeometricRouter.Vertices.ToList(), rootVertex, 10, 190, 350,
                    false);

                Assert.AreEqual(1, HybridGeometricRouter.Wavelets.Count);
                var wavelet = ToList(HybridGeometricRouter.Wavelets)[0];
                Assert.AreEqual(190, wavelet.FromAngle);
                Assert.AreEqual(350, wavelet.ToAngle);
                Assert.AreEqual(2, wavelet.RelevantVertices.Count);
                Assert.AreEqual(rootVertex, wavelet.RootVertex);
            }

            [Test]
            public void AngleRangeExceedsZeroDegree()
            {
                Assert.True(HybridGeometricRouter.Wavelets.IsEmpty());

                HybridGeometricRouter.AddNewWavelet(HybridGeometricRouter.Vertices.ToList(), rootVertex, 10, 190, 90,
                    false);

                Assert.AreEqual(2, HybridGeometricRouter.Wavelets.Count);

                var wavelets = ToList(HybridGeometricRouter.Wavelets);
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
            static HybridGeometricRouter _hybridGeometricRouter;
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

                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void RouteDirectlyToTarget()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 1.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 1.5);

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
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

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
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

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
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

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
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

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
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
            static HybridGeometricRouter _hybridGeometricRouter;

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
                    new Coordinate(5.5, 1),
                    new Coordinate(6, 1),
                    new Coordinate(6, 2)
                })); // -> "u" with additional collinear vertex in the middle
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(2, 0.5),
                    new Coordinate(2, 1)
                })); // -> making the square int a "q"
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(5.5, 0.5),
                    new Coordinate(5.5, 1)
                })); // -> making the u into a kind of "y"
                obstacleGeometries.Add(new LineString(new[]
                {
                    new Coordinate(3, 0.5),
                    new Coordinate(3, 1)
                })); // -> make vertical part of "r" longer 

                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void RouteAlongTopAlignedObstacles()
            {
                var sourceVertex = Position.CreateGeoPosition(0, 1.9);
                var targetVertex = Position.CreateGeoPosition(7, 1.9);

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
                var routeCoordinates = routingResult.OptimalRoute.Map(w => w.Position.ToCoordinate());

                var expectedRoute = new List<Coordinate>()
                {
                    sourceVertex.ToCoordinate(),
                    new Coordinate(1, 2),
                    new Coordinate(3, 2),
                    new Coordinate(4, 2),
                    new Coordinate(5, 2),
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

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
                var routeCoordinates = routingResult.OptimalRoute.Map(w => w.Position.ToCoordinate());

                var expectedRoute = new List<Coordinate>()
                {
                    sourceVertex.ToCoordinate(),
                    new Coordinate(1, 1),
                    new Coordinate(2, 0.5),
                    new Coordinate(3, 0.5),
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

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
                var routeCoordinates = routingResult.OptimalRoute.Map(w => w.Position.ToCoordinate());

                var expectedRoute = new List<Coordinate>()
                {
                    sourceVertex.ToCoordinate(),
                    targetVertex.ToCoordinate()
                };

                CollectionAssert.AreEqual(expectedRoute, routeCoordinates);
            }
        }

        public class RouteIntoOpenObstacle
        {
            static HybridGeometricRouter _hybridGeometricRouter;

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

                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void RouteIntoObstacle()
            {
                var sourceVertex = Position.CreateGeoPosition(0.8, -0.8);
                var targetVertex = Position.CreateGeoPosition(0.8, 0.5);

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
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
            static HybridGeometricRouter _hybridGeometricRouter;

            [SetUp]
            public void Setup()
            {
                _hybridGeometricRouter = new HybridGeometricRouter(new List<Feature>());
            }

            [Test]
            public void RouteDirectlyToTarget()
            {
                var sourceVertex = Position.CreateGeoPosition(3.5, 1.5);
                var targetVertex = Position.CreateGeoPosition(1.5, 1.5);

                var routingResult = _hybridGeometricRouter.Route(sourceVertex, targetVertex);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.Contains(sourceVertex, waypoints);
                Assert.Contains(targetVertex, waypoints);
                Assert.AreEqual(2, waypoints.Count);
            }
        }

        public class ZickZackAroundBuildings
        {
            // Real world problem: Route was not shortest when going around building

            static HybridGeometricRouter _hybridGeometricRouter;

            [SetUp]
            public void Setup()
            {
                var eastBuilding = new Polygon(new LinearRing(new[]
                {
                    new Coordinate(1, 2.5),
                    new Coordinate(2.5, 1),
                    new Coordinate(3.5, 2),
                    new Coordinate(2, 3.5),
                    new Coordinate(1, 2.5)
                }));
                var westBuilding = new Polygon(new LinearRing(new[]
                {
                    new Coordinate(3, 3.5),
                    new Coordinate(5, 1.5),
                    new Coordinate(6.5, 3),
                    new Coordinate(4.5, 5),
                    new Coordinate(3, 3.5)
                }));
                var obstacleGeometries = new List<NetTopologySuite.Geometries.Geometry>();
                obstacleGeometries.Add(eastBuilding);
                obstacleGeometries.Add(westBuilding);

                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void Routing()
            {
                var routingResult = _hybridGeometricRouter.Route(Position.CreateGeoPosition(2, 1),
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

            private HybridGeometricRouter _hybridGeometricRouter;

            [SetUp]
            public void Setup()
            {
                var obstacle = new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(3, 0),
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

                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void TargetWithinObstacle()
            {
                var source = Position.CreateGeoPosition(1, 2);
                var target = Position.CreateGeoPosition(2, 0.25);
                var routingResult = _hybridGeometricRouter.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(0, waypoints.Count);
            }
        }

        public class OverlappingObstacles_TouchingLines
        {
            // Real world problem: Route was going through the vertex where two lines touched

            private HybridGeometricRouter _hybridGeometricRouter;
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

                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void RouteAroundTouchingLines()
            {
                var source = Position.CreateGeoPosition(0.2, 2.25);
                var target = Position.CreateGeoPosition(0.2, 0.5);
                var routingResult = _hybridGeometricRouter.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(5, waypoints.Count);

                Assert.AreEqual(source, waypoints[0]);
                Assert.AreEqual(line1[2].ToPosition(), waypoints[1]);
                Assert.AreEqual(line1[1].ToPosition(), waypoints[2]);
                Assert.AreEqual(line1[0].ToPosition(), waypoints[3]);
                Assert.AreEqual(target, waypoints[4]);
            }

            [Test]
            public void HandlingCollinearVertex_CreatesNewWavelet()
            {
                var nextVertex = new Vertex(line1[1].ToPosition(), line1[0].ToPosition(), line1[2].ToPosition(),
                    line2[1].ToPosition());
                var relevantVertices = new List<Vertex>();
                relevantVertices.Add(nextVertex);

                var rootVertex = new Vertex(line1[2].ToPosition(), line1[1].ToPosition(), line1[3].ToPosition());
                var wavelet = Wavelet.New(180, 270, rootVertex, relevantVertices, 1, false)!;
                _hybridGeometricRouter.AddWavelet(wavelet);

                _hybridGeometricRouter.ProcessNextEvent(new Position(-1, 0), new Stopwatch());

                Assert.AreEqual(0, wavelet.RelevantVertices.Count);
                var wavelets = ToList(_hybridGeometricRouter.Wavelets);
                Assert.IsFalse(wavelets.Contains(wavelet));

                Assert.AreEqual(1, wavelets.Count);
                var newWavelet = wavelets[0];
                Assert.AreEqual(1, newWavelet.RelevantVertices.Count);
                Assert.AreEqual(line1[0], newWavelet.RelevantVertices.ToList()[0].Coordinate);
                Assert.AreEqual(180, newWavelet.FromAngle);
                Assert.AreEqual(180, newWavelet.ToAngle);
            }

            [Test]
            public void RouteIntoClosedObstacle()
            {
                var source = Position.CreateGeoPosition(0.2, 2.25);
                var target = Position.CreateGeoPosition(0.2, 1.5);
                var routingResult = _hybridGeometricRouter.Route(source, target);
                var waypoints = routingResult.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(0, waypoints.Count);
            }
        }

        public class OverlappingObstacles_PolygonSharingEdge
        {
            private List<Feature> obstacles;
            private HybridGeometricRouter _hybridGeometricRouter;
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

                obstacles = new List<Feature>();
                obstacles.Add(new Feature(obstacle1, new AttributesTable()));
                obstacles.Add(new Feature(obstacle2, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);

                start = Position.CreateGeoPosition(3, 5);
                target = Position.CreateGeoPosition(0, 3);
            }

            [Test]
            public void ShouldNotRouteAlongTouchingEdge()
            {
                var result = _hybridGeometricRouter.Route(start, target);
                var waypoints = result.OptimalRoute.Map(w => w.Position);

                Assert.AreEqual(start, waypoints[0]);
                Assert.AreEqual(obstacle1[3].ToPosition(), waypoints[1]);
                Assert.AreEqual(obstacle1[2].ToPosition(), waypoints[2]);
                Assert.AreEqual(obstacle2[3].ToPosition(), waypoints[3]);
                Assert.AreEqual(obstacle2[0].ToPosition(), waypoints[4]);
                Assert.AreEqual(target, waypoints[5]);
                Assert.AreEqual(6, waypoints.Count);
            }
        }

        public class TouchingLineEnds
        {
            // Real world problem: Route was going through the vertex where two lines touched

            private HybridGeometricRouter _hybridGeometricRouter;
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

                var obstacles = obstacleGeometries.Map(geometry => new Feature(geometry, new AttributesTable()));

                _hybridGeometricRouter = new HybridGeometricRouter(obstacles);
            }

            [Test]
            public void RouteBelowTouchingLineEnds()
            {
                var source = Position.CreateGeoPosition(0.1, 1.2);
                var target = Position.CreateGeoPosition(0.1, -0.1);
                var routingResult = _hybridGeometricRouter.Route(source, target);
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
                var routingResult = _hybridGeometricRouter.Route(source, target);
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