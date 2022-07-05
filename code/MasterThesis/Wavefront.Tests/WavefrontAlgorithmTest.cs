using System.Collections.Generic;
using Mars.Numerics;
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

        [Test]
        public void GetEnclosingAngle()
        {
            double from;
            double to;

            WavefrontAlgorithm.GetEnclosingAngles(10, 200, out from, out to);
            Assert.AreEqual(200, from);
            Assert.AreEqual(10, to);

            WavefrontAlgorithm.GetEnclosingAngles(10, 90, out from, out to);
            Assert.AreEqual(10, from);
            Assert.AreEqual(90, to);
        }

        public class WithWavefrontAlgorithm
        {
            static WavefrontAlgorithm wavefrontAlgorithm;
            static List<Vertex> vertices;
            static Vertex rootVertex;
            static List<Wavefront> wavefronts;

            [SetUp]
            public void Setup()
            {
                wavefronts = new List<Wavefront>();

                wavefrontAlgorithm =
                    new WavefrontAlgorithm(new List<NetTopologySuite.Geometries.Geometry>(), wavefronts);

                vertices = new List<Vertex>();
                vertices.Add(new Vertex(Position.CreateGeoPosition(0, 0)));
                vertices.Add(new Vertex(Position.CreateGeoPosition(1, 1)));
                vertices.Add(new Vertex(Position.CreateGeoPosition(5.1, 3)));

                rootVertex = new Vertex(Position.CreateGeoPosition(5, 2));
            }

            public class AdjustWavefront : WithWavefrontAlgorithm
            {
                Wavefront wavefront;

                [SetUp]
                public void Setup()
                {
                    wavefront = Wavefront.newIfValid(10, 350, rootVertex, vertices, 10)!;
                    wavefronts.Add(wavefront);
                }

                [Test]
                public void FromLowerToAngle()
                {
                    Assert.AreEqual(1, wavefronts.Count);
                    Assert.True(wavefronts.Contains(wavefront));

                    wavefrontAlgorithm.AdjustWavefront(vertices, rootVertex, 5, 50, 250, wavefront);

                    Assert.AreEqual(1, wavefronts.Count);
                    Assert.False(wavefronts.Contains(wavefront));
                }

                [Test]
                public void FromLargerToAngle()
                {
                    Assert.AreEqual(1, wavefronts.Count);
                    Assert.True(wavefronts.Contains(wavefront));

                    wavefrontAlgorithm.AdjustWavefront(vertices, rootVertex, 5, 250, 50, wavefront);

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
                    wavefrontAlgorithm.AddWavefrontIfValid(vertices, 10, rootVertex, 10, 10);
                    Assert.True(wavefronts.IsEmpty());
                }

                [Test]
                public void NewWavefrontWouldBeInvalid()
                {
                    Assert.True(wavefronts.IsEmpty());
                    wavefrontAlgorithm.AddWavefrontIfValid(vertices, 10, rootVertex, 10, 11);
                    Assert.True(wavefronts.IsEmpty());
                }

                [Test]
                public void NewWavefrontAdded()
                {
                    Assert.True(wavefronts.IsEmpty());
                    wavefrontAlgorithm.AddWavefrontIfValid(vertices, 10, rootVertex, 190, 0);
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
                // TODO 
            }

            public class HandleNeighborVertex : WithWavefrontAlgorithm
            {
                // TODO 
            }
        }
    }
}