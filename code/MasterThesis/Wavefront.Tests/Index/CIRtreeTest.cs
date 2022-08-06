using System.Collections.Generic;
using NUnit.Framework;
using Wavefront.Index;

namespace Wavefront.Tests.Index;

public class CIRtreeTest
{
    private CITree<string> _tree = new();

    [Test]
    public void Query_exceedingZeroDegree()
    {
        _tree.Insert(20, 50, "20-50");
        _tree.Insert(350, 30, "350-30");
        _tree.Insert(330, 360, "330-360");
        _tree.Insert(0, 50, "0-50");

        IList<string> result;
        result = _tree.Query(320);
        Assert.AreEqual(0, result.Count);
        
        result = _tree.Query(335);
        Assert.AreEqual(1, result.Count);
        
        result = _tree.Query(355);
        Assert.AreEqual(2, result.Count);
        
        result = _tree.Query(360);
        Assert.AreEqual(4, result.Count);
        
        result = _tree.Query(20);
        Assert.AreEqual(3, result.Count);
        
        result = _tree.Query(50);
        Assert.AreEqual(2, result.Count);
        
        result = _tree.Query(55);
        Assert.AreEqual(0, result.Count);
    }
}