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

        var wavefront = Wavefront.New(0, 90, root, vertices, 1);
        Assert.AreEqual(vertices[0], wavefront.GetNextVertex());
        Assert.AreEqual(2, wavefront.DistanceToNextVertex);

        wavefront.RemoveNextVertex();
        Assert.AreEqual(vertices[1], wavefront.GetNextVertex());
        Assert.AreEqual(3, wavefront.DistanceToNextVertex);
    }
}