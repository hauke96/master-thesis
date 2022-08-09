using System.Collections.Generic;
using NUnit.Framework;
using static Wavefront.WavefrontPreprocessor;

namespace Wavefront.Tests;

public class WavefrontPreprocessorTest
{
    [Test]
    public void Merge()
    {
        var areas = new List<AngleArea>()
        {
            new AngleArea(0, 40, 1),
            new AngleArea(50, 100, 1),
            new AngleArea(300, 360, 1),
        };

        double from;
        double to;
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 10);
        Assert.AreEqual(0, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 30);
        Assert.AreEqual(0, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 60);
        Assert.AreEqual(0, from);
        Assert.AreEqual(100, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 320);
        Assert.AreEqual(0, from);
        Assert.AreEqual(360, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 350, 10);
        Assert.AreEqual(300, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 350, 360);
        Assert.AreEqual(300, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 150, 200);
        Assert.AreEqual(150, from);
        Assert.AreEqual(200, to);
    }
    
    [Test]
    public void Merge_WithZeroDegreeOverlap()
    {
        var areas = new List<AngleArea>()
        {
            new AngleArea(350, 40, 1),
            new AngleArea(50, 100, 1),
            new AngleArea(300, 340, 1),
        };

        double from;
        double to;
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 10);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 30);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 60);
        Assert.AreEqual(350, from);
        Assert.AreEqual(100, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 10, 320);
        Assert.AreEqual(350, from);
        Assert.AreEqual(340, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 350, 10);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 350, 360);
        Assert.AreEqual(350, from);
        Assert.AreEqual(40, to);
        
        (from, to) = WavefrontPreprocessor.Merge(areas, 150, 200);
        Assert.AreEqual(150, from);
        Assert.AreEqual(200, to);
    }
}