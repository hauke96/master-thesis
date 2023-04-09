using System;
using System.Collections.Generic;
using HybridVisibilityGraphRouting.Index;
using NUnit.Framework;

namespace HybridVisibilityGraphRouting.Tests.Index;

public class BinIndexTest
{
    [Test]
    public void Add()
    {
        var binIndex = new BinIndex<string>(10);
        var value48 = "4-8";
        binIndex.Add(4, 8, value48);
        CollectionAssert.IsEmpty(binIndex.Query(0));
        CollectionAssert.IsEmpty(binIndex.Query(1));
        CollectionAssert.IsEmpty(binIndex.Query(2));
        CollectionAssert.IsEmpty(binIndex.Query(3));
        CollectionAssert.AreEquivalent(new List<string> { value48 }, binIndex.Query(4));
        CollectionAssert.AreEquivalent(new List<string> { value48 }, binIndex.Query(5));
        CollectionAssert.AreEquivalent(new List<string> { value48 }, binIndex.Query(6));
        CollectionAssert.AreEquivalent(new List<string> { value48 }, binIndex.Query(7));
        CollectionAssert.AreEquivalent(new List<string> { value48 }, binIndex.Query(8));
        CollectionAssert.IsEmpty(binIndex.Query(9));
        CollectionAssert.IsEmpty(binIndex.Query(10));
    }

    [Test]
    public void Add_ringProperty()
    {
        var binIndex = new BinIndex<string>(10);
        var value82 = "8-2";
        binIndex.Add(8, 2, value82);
        CollectionAssert.AreEquivalent(new List<string> { value82 }, binIndex.Query(0));
        CollectionAssert.AreEquivalent(new List<string> { value82 }, binIndex.Query(1));
        CollectionAssert.AreEquivalent(new List<string> { value82 }, binIndex.Query(2));
        CollectionAssert.IsEmpty(binIndex.Query(3));
        CollectionAssert.IsEmpty(binIndex.Query(4));
        CollectionAssert.IsEmpty(binIndex.Query(5));
        CollectionAssert.IsEmpty(binIndex.Query(6));
        CollectionAssert.IsEmpty(binIndex.Query(7));
        CollectionAssert.AreEquivalent(new List<string> { value82 }, binIndex.Query(8));
        CollectionAssert.AreEquivalent(new List<string> { value82 }, binIndex.Query(9));
        CollectionAssert.AreEquivalent(new List<string> { value82 }, binIndex.Query(10));
    }

    [Test]
    public void Add_invalidEntries()
    {
        var binIndex = new BinIndex<string>(10);

        Assert.Throws<ArgumentException>(() => binIndex.Add(-1, 5, "foo"));
        Assert.Throws<ArgumentException>(() => binIndex.Add(5, 11, "foo"));
        Assert.Throws<ArgumentException>(() => binIndex.Add(-2, -1, "foo"));
        Assert.Throws<ArgumentException>(() => binIndex.Add(11, 12, "foo"));
    }

    [Test]
    public void Query()
    {
        var binIndex = new BinIndex<string>(10);
        binIndex.Add(0, 10, "0-10");
        binIndex.Add(0, 0, "0-0");
        binIndex.Add(0, 5, "0-5");
        binIndex.Add(5, 5, "5-5");
        binIndex.Add(5, 10, "5-10");
        binIndex.Add(10, 10, "10-10");

        LinkedList<string> result;

        result = binIndex.Query(0);
        Assert.AreEqual(3, result.Count);
        CollectionAssert.Contains(result, "0-0");
        CollectionAssert.Contains(result, "0-5");
        CollectionAssert.Contains(result, "0-10");

        result = binIndex.Query(5);
        Assert.AreEqual(4, result.Count);
        CollectionAssert.Contains(result, "0-5");
        CollectionAssert.Contains(result, "5-5");
        CollectionAssert.Contains(result, "5-10");
        CollectionAssert.Contains(result, "0-10");

        result = binIndex.Query(10);
        Assert.AreEqual(3, result.Count);
        CollectionAssert.Contains(result, "0-10");
        CollectionAssert.Contains(result, "5-10");
        CollectionAssert.Contains(result, "10-10");
    }

    [Test]
    public void Query_invalidEntries()
    {
        var binIndex = new BinIndex<string>(10);

        Assert.Throws<ArgumentException>(() => binIndex.Query(-1));
        Assert.Throws<ArgumentException>(() => binIndex.Query(11));
    }
}