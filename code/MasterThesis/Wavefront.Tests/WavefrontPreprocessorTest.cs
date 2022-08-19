using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;
using Wavefront.Index;

namespace Wavefront.Tests;

public class WavefrontPreprocessorTest
{
    [Test]
    public void GetEnvelopesForIndex_indexZero()
    {
        var envelopes = WavefrontPreprocessor.GetEnvelopesForIndex(new Coordinate(0, 0), 0).ToList();

        Assert.AreEqual(2, envelopes.Count);
        Assert.AreEqual(
            new Envelope(-WavefrontPreprocessor.BaseLength, WavefrontPreprocessor.BaseLength,
                WavefrontPreprocessor.BaseLength, 0), envelopes[0]);
        Assert.AreEqual(
            new Envelope(-WavefrontPreprocessor.BaseLength, WavefrontPreprocessor.BaseLength, 0,
                -WavefrontPreprocessor.BaseLength), envelopes[1]);
    }

    [Test]
    public void GetEnvelopesForIndex_indexOne()
    {
        var i = 1;
        var envelopes = WavefrontPreprocessor.GetEnvelopesForIndex(new Coordinate(0, 0), i).ToList();

        Assert.AreEqual(4, envelopes.Count);
        Assert.AreEqual(
            new Envelope(-(i + 1) * WavefrontPreprocessor.BaseLength, (i + 1) * WavefrontPreprocessor.BaseLength,
                (i + 1) * WavefrontPreprocessor.BaseLength, i * WavefrontPreprocessor.BaseLength), envelopes[0]);
        Assert.AreEqual(
            new Envelope(-(i + 1) * WavefrontPreprocessor.BaseLength, (i + 1) * WavefrontPreprocessor.BaseLength,
                -i * WavefrontPreprocessor.BaseLength, -(i + 1) * WavefrontPreprocessor.BaseLength), envelopes[1]);
        Assert.AreEqual(
            new Envelope((i + 1) * WavefrontPreprocessor.BaseLength, i * WavefrontPreprocessor.BaseLength,
                i * WavefrontPreprocessor.BaseLength, -i * WavefrontPreprocessor.BaseLength), envelopes[2]);
        Assert.AreEqual(
            new Envelope(-(i + 1) * WavefrontPreprocessor.BaseLength, -i * WavefrontPreprocessor.BaseLength,
                i * WavefrontPreprocessor.BaseLength, -i * WavefrontPreprocessor.BaseLength), envelopes[3]);
    }

    [Test]
    public void GetEnvelopesForIndex_indexTwo()
    {
        var i = 2;
        var envelopes = WavefrontPreprocessor.GetEnvelopesForIndex(new Coordinate(0, 0), i).ToList();

        Assert.AreEqual(4, envelopes.Count);
        Assert.AreEqual(
            new Envelope(-(i + 1) * WavefrontPreprocessor.BaseLength, (i + 1) * WavefrontPreprocessor.BaseLength,
                (i + 1) * WavefrontPreprocessor.BaseLength, i * WavefrontPreprocessor.BaseLength), envelopes[0]);
        Assert.AreEqual(
            new Envelope(-(i + 1) * WavefrontPreprocessor.BaseLength, (i + 1) * WavefrontPreprocessor.BaseLength,
                -i * WavefrontPreprocessor.BaseLength, -(i + 1) * WavefrontPreprocessor.BaseLength), envelopes[1]);
        Assert.AreEqual(
            new Envelope((i + 1) * WavefrontPreprocessor.BaseLength, i * WavefrontPreprocessor.BaseLength,
                i * WavefrontPreprocessor.BaseLength, -i * WavefrontPreprocessor.BaseLength), envelopes[2]);
        Assert.AreEqual(
            new Envelope(-(i + 1) * WavefrontPreprocessor.BaseLength, -i * WavefrontPreprocessor.BaseLength,
                i * WavefrontPreprocessor.BaseLength, -i * WavefrontPreprocessor.BaseLength), envelopes[3]);
    }


    public class WithWavefrontAlgorithm : WavefrontTestHelper.WithWavefrontAlgorithm
    {
        [Test]
        public void IsVertexVisible()
        {
            Assert.True(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, multiVertexLineVertices[0],
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                multiVertexLineVertices[1]));
            Assert.True(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, multiVertexLineVertices[0],
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                multiVertexLineVertices[2]));
            Assert.True(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, multiVertexLineVertices[0],
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                simpleLineVertices[0]));
            Assert.True(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, multiVertexLineVertices[0],
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                simpleLineVertices[1]));

            Assert.True(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, new Vertex(0, 5.95),
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                simpleLineVertices[0]));
            Assert.True(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, new Vertex(0, 5.95),
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                simpleLineVertices[1]));
            Assert.True(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, new Vertex(0, 5.95),
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                multiVertexLineVertices[0]));
            Assert.False(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, new Vertex(0, 5.95),
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                multiVertexLineVertices[1]));
            Assert.False(WavefrontPreprocessor.IsVertexVisible(wavefrontAlgorithm._obstacles, new Vertex(0, 5.95),
                new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                multiVertexLineVertices[2]));
        }

        [Test]
        public void FindKnnVertices()
        {
            var neighborList = new List<Vertex>();
            WavefrontPreprocessor.FindKnnVertices(wavefrontAlgorithm._obstacles, wavefrontAlgorithm.Vertices,
                multiVertexLineVertices[0], new BinIndex<WavefrontPreprocessor.AngleArea>(360), new HashSet<Obstacle>(),
                neighborList, 100);
            Assert.AreEqual(4, neighborList.Count);
            Assert.Contains(multiVertexLineVertices[1], neighborList);
            Assert.Contains(multiVertexLineVertices[2], neighborList);
            Assert.Contains(simpleLineVertices[0], neighborList);
            Assert.Contains(simpleLineVertices[1], neighborList);
        }

        [Test]
        public void FindKnnVertices_multipleOtherVertices()
        {
            var neighborList = new List<Vertex>();
            var shadowAreas = new BinIndex<WavefrontPreprocessor.AngleArea>(360);
            var obstaclesCastingShadow = new HashSet<Obstacle>();

            WavefrontPreprocessor.FindKnnVertices(wavefrontAlgorithm._obstacles,
                new List<Vertex> { simpleLineVertices[0] }, multiVertexLineVertices[0], shadowAreas,
                obstaclesCastingShadow, neighborList, 100);
            WavefrontPreprocessor.FindKnnVertices(wavefrontAlgorithm._obstacles,
                new List<Vertex> { simpleLineVertices[1] }, multiVertexLineVertices[0], shadowAreas,
                obstaclesCastingShadow, neighborList, 100);
            WavefrontPreprocessor.FindKnnVertices(wavefrontAlgorithm._obstacles,
                new List<Vertex> { multiVertexLineVertices[1] }, multiVertexLineVertices[0], shadowAreas,
                obstaclesCastingShadow, neighborList, 100);
            WavefrontPreprocessor.FindKnnVertices(wavefrontAlgorithm._obstacles,
                new List<Vertex> { multiVertexLineVertices[2] }, multiVertexLineVertices[0], shadowAreas,
                obstaclesCastingShadow, neighborList, 100);

            Assert.AreEqual(4, neighborList.Count);
            Assert.Contains(multiVertexLineVertices[1], neighborList);
            Assert.Contains(multiVertexLineVertices[2], neighborList);
            Assert.Contains(simpleLineVertices[0], neighborList);
            Assert.Contains(simpleLineVertices[1], neighborList);
        }

        [Test]
        public void GetVisibleNeighborsForVertex()
        {
            var vertexTree = new STRtree<Vertex>();
            vertices.Each(v => vertexTree.Insert(new Envelope(v.Coordinate), v));

            var visibleVertices = WavefrontPreprocessor.GetVisibleNeighborsForVertex(wavefrontAlgorithm._obstacles,
                vertexTree, multiVertexLineVertices[0], 100);

            Assert.AreEqual(4, visibleVertices.Count);
            Assert.Contains(multiVertexLineVertices[1], visibleVertices);
            Assert.Contains(multiVertexLineVertices[2], visibleVertices);
            Assert.Contains(simpleLineVertices[0], visibleVertices);
            Assert.Contains(simpleLineVertices[1], visibleVertices);
        }
    }
}