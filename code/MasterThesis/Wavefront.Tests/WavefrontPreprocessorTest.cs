using System.Collections.Generic;
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
        Assert.AreEqual(0, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(3, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 30, 0.5);
        Assert.AreEqual(0, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(3, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 60, 10);
        Assert.AreEqual(0, from);
        Assert.AreEqual(100, to);
        Assert.AreEqual(10, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 320, 5);
        Assert.AreEqual(0, from);
        Assert.AreEqual(360, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 10, 5);
        Assert.AreEqual(300, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 360, 5);
        Assert.AreEqual(300, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 150, 200, 5);
        Assert.AreEqual(150, from);
        Assert.AreEqual(200, to);
        Assert.AreEqual(5, distance);
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
        Assert.AreEqual(3, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 30, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 60, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(100, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 10, 320, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(340, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 10, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 350, 360, 5);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        Assert.AreEqual(5, distance);

        (from, to, distance) = WavefrontPreprocessor.Merge(areas, 150, 200, 5);
        Assert.AreEqual(150, from);
        Assert.AreEqual(200, to);
        Assert.AreEqual(5, distance);
    }
}