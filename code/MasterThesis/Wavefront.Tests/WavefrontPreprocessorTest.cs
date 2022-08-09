using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wavefront.Index;
using static Wavefront.WavefrontPreprocessor;

namespace Wavefront.Tests;

public class WavefrontPreprocessorTest
{
    private CITreeNode<AngleArea> newAngleArea(double from, double to, double distance)
    {
        return new CITreeNode<AngleArea>(from, to, new AngleArea(from, to, distance));
    }

    [Test]
    public void Merge()
    {
        var areas = new List<CITreeNode<AngleArea>>()
        {
            newAngleArea(0, 40, 1),
            newAngleArea(50, 100, 2),
            newAngleArea(300, 360, 3),
        };

        double from;
        double to;
        double distance;

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 10, 2);
        Assert.AreEqual(10, from);
        Assert.AreEqual(100, to);
        Assert.AreEqual(2, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 30, 0.5);
        Assert.AreEqual(0, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(0.5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 60, 10);
        Assert.AreEqual(0, from);
        Assert.AreEqual(100, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 320, 5);
        Assert.AreEqual(0, from);
        Assert.AreEqual(360, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 10, 5);
        Assert.AreEqual(300, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 360, 5);
        Assert.AreEqual(300, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 150, 200, 5);
        Assert.AreEqual(150, from);
        Assert.AreEqual(200, to);
        Assert.AreEqual(1, distance);
    }

    [Test]
    public void Merge_WithZeroDegreeOverlap()
    {
        var areas = new List<CITreeNode<AngleArea>>()
        {
            newAngleArea(350, 40, 1),
            newAngleArea(50, 100, 2),
            newAngleArea(300, 340, 3),
        };

        double from;
        double to;
        double distance;

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 10, 0);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(0, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 30, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 60, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(100, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 320, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(340, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 10, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 360, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(1, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 150, 200, 5);
        Assert.AreEqual(150, from);
        Assert.AreEqual(200, to);
        Assert.AreEqual(1, distance);
    }

    [Test]
    public void Relax()
    {
        var tree = new CITree<AngleArea>();
        tree.Insert(350, 40, new AngleArea(350, 40, 1));
        tree.Insert(20, 50, new AngleArea(20, 50, 2));
        tree.Insert(60, 80, new AngleArea(60, 80, 2));
        tree.Insert(65, 70, new AngleArea(65, 70, 1));
        tree.Insert(70, 90, new AngleArea(70, 90, 5));

        var maxDistance = 3;
        RelaxShadowAreas(tree, maxDistance);

        var nodes = tree.QueryAll().ToList();
        Assert.AreEqual(4, nodes.Count);

        CITreeNode<AngleArea> node;
        
        node = nodes[0];
        Assert.AreEqual(60, node.From);
        Assert.AreEqual(80, node.To);
        Assert.AreEqual(node.From, node.Value.From);
        Assert.AreEqual(node.To, node.Value.To);
        Assert.AreEqual(maxDistance, node.Value.Distance);
        
        node = nodes[1];
        Assert.AreEqual(0, node.From);
        Assert.AreEqual(50, node.To);
        Assert.AreEqual(350, node.Value.From);
        Assert.AreEqual(node.To, node.Value.To);
        Assert.AreEqual(maxDistance, node.Value.Distance);
        
        node = nodes[2];
        Assert.AreEqual(70, node.From);
        Assert.AreEqual(90, node.To);
        Assert.AreEqual(node.From, node.Value.From);
        Assert.AreEqual(node.To, node.Value.To);
        Assert.AreEqual(5, node.Value.Distance);
        
        node = nodes[3];
        Assert.AreEqual(350, node.From);
        Assert.AreEqual(360, node.To);
        Assert.AreEqual(node.From, node.Value.From);
        Assert.AreEqual(50, node.Value.To);
        Assert.AreEqual(maxDistance, node.Value.Distance);
    }
}