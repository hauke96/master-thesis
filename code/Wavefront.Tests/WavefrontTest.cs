using System.Collections.Generic;
using NUnit.Framework;
using Wavefront.Geometry;

namespace Wavefront.Tests;

public class WavefrontTest
{
    [Test]
    public void NewWavelet_WithZeroDegreeNeighbor()
    {
        // Real world problem: When the to-angle of the new wavelet reached to 360° and one neighbor is at 0°, this
        // neighbor will be ignored even though it's reachable by the wavelet.
        
        var vertices = new List<Vertex>();
        vertices.Add(new Vertex(1, 1));
        vertices.Add(new Vertex(1, 2));
        vertices.Add(new Vertex(0, 2));
        var root = vertices[0];

        var wavelet = Wavelet.New(270, 360, root, vertices, 1, false);
        Assert.Contains(vertices[1], wavelet.RelevantVertices);
        Assert.Contains(vertices[2], wavelet.RelevantVertices);
    }
    
    [Test]
    public void NewWavelet_360Degree()
    {
        // Real world problem: When the to-angle of the new wavelet reached to 360° and one neighbor is at 0°, this
        // neighbor will be ignored even though it's reachable by the wavelet.
        
        var vertices = new List<Vertex>();
        vertices.Add(new Vertex(1, 1));
        vertices.Add(new Vertex(1, 2));
        vertices.Add(new Vertex(0, 2));
        var root = vertices[0];

        var wavelet = Wavelet.New(0, 360, root, vertices, 1, false);
        Assert.Contains(vertices[1], wavelet.RelevantVertices);
        Assert.Contains(vertices[2], wavelet.RelevantVertices);
    }
    
    [Test]
    public void RemoveNextVertex()
    {
        var root = new Vertex(0, 1);
        var vertices = new List<Vertex>();
        vertices.Add(new Vertex(1, 1));
        vertices.Add(new Vertex(2, 1));
        vertices.Add(new Vertex(3, 1));

        var wavelet = Wavelet.New(0, 90, root, vertices, 1, false);
        Assert.AreEqual(vertices[0], wavelet.GetNextVertex());
        Assert.AreEqual(2, wavelet.DistanceToNextVertex);

        wavelet.RemoveNextVertex();
        Assert.AreEqual(vertices[1], wavelet.GetNextVertex());
        Assert.AreEqual(3, wavelet.DistanceToNextVertex);
    }
}