using System.Collections.Generic;
using NUnit.Framework;
using Wavefront.Geometry;

namespace Wavefront.Tests;

public class WavefrontTest
{
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