using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wavefront.Index;

namespace Wavefront.Tests.Index;

public class BinIndexTest
{
    [Test]
    public void GetIndexFromKey()
    {
        var index = new BinIndex<string>(0d, 3d);

        Assert.AreEqual(4, index.BinCount);
        Assert.AreEqual(0, index.GetIndexFromKey(0));
        Assert.AreEqual(1, index.GetIndexFromKey(1));
        Assert.AreEqual(2, index.GetIndexFromKey(2));
        Assert.AreEqual(3, index.GetIndexFromKey(3));

        Assert.AreEqual(0, index.GetIndexFromKey(0.999));
        Assert.AreEqual(1, index.GetIndexFromKey(1.001));
        Assert.AreEqual(1, index.GetIndexFromKey(1.999));
        Assert.AreEqual(2, index.GetIndexFromKey(2.001));
        Assert.AreEqual(2, index.GetIndexFromKey(2.999));
        Assert.AreEqual(3, index.GetIndexFromKey(3.001));
        Assert.AreEqual(3, index.GetIndexFromKey(3.999));
    }

    [Test]
    public void GetIndexFromKey_largerBinSize()
    {
        var index = new BinIndex<string>(0d, 3d, 5);

        Assert.AreEqual(16, index.BinCount);
        Assert.AreEqual(0, index.GetIndexFromKey(0));
        Assert.AreEqual(1, index.GetIndexFromKey(0.2));
        Assert.AreEqual(4, index.GetIndexFromKey(0.8));
        Assert.AreEqual(5, index.GetIndexFromKey(1));
        Assert.AreEqual(10, index.GetIndexFromKey(2));
        Assert.AreEqual(11, index.GetIndexFromKey(2.2));
        Assert.AreEqual(14, index.GetIndexFromKey(2.8));
        Assert.AreEqual(15, index.GetIndexFromKey(3));
    }

    [Test]
    public void GetIndexFromKey_smallerBinSize()
    {
        var index = new BinIndex<string>(0d, 30d, 0.2);

        Assert.AreEqual(7, index.BinCount);
        Assert.AreEqual(0, index.GetIndexFromKey(0));
        Assert.AreEqual(0, index.GetIndexFromKey(1));
        Assert.AreEqual(0, index.GetIndexFromKey(4));
        Assert.AreEqual(1, index.GetIndexFromKey(5));
        Assert.AreEqual(1, index.GetIndexFromKey(9));
        Assert.AreEqual(2, index.GetIndexFromKey(10));
        Assert.AreEqual(4, index.GetIndexFromKey(20));
        Assert.AreEqual(4, index.GetIndexFromKey(21));
        Assert.AreEqual(4, index.GetIndexFromKey(24));
        Assert.AreEqual(5, index.GetIndexFromKey(25));
        Assert.AreEqual(5, index.GetIndexFromKey(29));
        Assert.AreEqual(6, index.GetIndexFromKey(30));
    }

    [Test]
    public void AddAndQueryHappyPaths()
    {
        var index = new BinIndex<string>(0d, 3d);
        Assert.AreEqual(4, index.BinCount);

        var item00 = "00";
        var item01 = "01";
        var item12 = "12";
        var item22 = "22";
        index.Add(0, 0, item00);
        index.Add(0, 1, item01);
        index.Add(1, 2, item12);
        index.Add(2, 2, item22);

        List<string> result;

        // Single keys
        result = index.Query(0).ToList();
        Assert.AreEqual(2, result.Count);
        Assert.Contains(item00, result);
        Assert.Contains(item01, result);

        result = index.Query(1).ToList();
        Assert.AreEqual(2, result.Count);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);

        result = index.Query(2).ToList();
        Assert.AreEqual(2, result.Count);
        Assert.Contains(item12, result);
        Assert.Contains(item22, result);

        result = index.Query(3).ToList();
        Assert.IsEmpty(result);

        // Range queries
        result = index.Query(0, 0).ToList();
        Assert.AreEqual(index.Query(0), result);
        result = index.Query(1, 1).ToList();
        Assert.AreEqual(index.Query(1), result);
        result = index.Query(2, 2).ToList();
        Assert.AreEqual(index.Query(2), result);

        result = index.Query(0, 1).ToList();
        Assert.AreEqual(3, result.Count);
        Assert.Contains(item00, result);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);

        result = index.Query(1, 2).ToList();
        Assert.AreEqual(3, result.Count);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);
        Assert.Contains(item22, result);

        result = index.Query(0, 2).ToList();
        Assert.AreEqual(4, result.Count);
        Assert.Contains(item00, result);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);
        Assert.Contains(item22, result);

        Assert.AreEqual(result, index.Query(0, 3).ToList());
    }

    [Test]
    public void AddAndQueryAsRing()
    {
        var index = new BinIndex<string>(0d, 2d, 1, true);
        Assert.AreEqual(3, index.BinCount);

        var item00 = "00";
        var item01 = "01";
        var item12 = "12";
        var item20 = "20";
        index.Add(0, 0, item00);
        index.Add(0, 1, item01);
        index.Add(1, 2, item12);
        index.Add(2, 0, item20);

        List<string> result;

        // Single keys
        result = index.Query(0).ToList();
        Assert.AreEqual(3, result.Count);
        Assert.Contains(item00, result);
        Assert.Contains(item01, result);
        Assert.Contains(item20, result);

        result = index.Query(1).ToList();
        Assert.AreEqual(2, result.Count);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);

        result = index.Query(2).ToList();
        Assert.AreEqual(2, result.Count);
        Assert.Contains(item12, result);
        Assert.Contains(item20, result);

        // Range queries
        result = index.Query(0, 0).ToList();
        Assert.AreEqual(index.Query(0), result);
        result = index.Query(1, 1).ToList();
        Assert.AreEqual(index.Query(1), result);
        result = index.Query(2, 2).ToList();
        Assert.AreEqual(index.Query(2), result);

        result = index.Query(0, 1).ToList();
        Assert.AreEqual(4, result.Count);
        Assert.Contains(item00, result);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);
        Assert.Contains(item20, result);

        result = index.Query(1, 2).ToList();
        Assert.AreEqual(3, result.Count);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);
        Assert.Contains(item20, result);

        result = index.Query(0, 2).ToList();
        Assert.AreEqual(4, result.Count);
        Assert.Contains(item00, result);
        Assert.Contains(item01, result);
        Assert.Contains(item12, result);
        Assert.Contains(item20, result);
    }
}