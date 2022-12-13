using System.Collections.Generic;
using NUnit.Framework;
using Wavefront.Index;

namespace Wavefront.Tests.Index;

public class BinIndexTest
{
    [Test]
    public void Add()
    {
        var binIndex = new BinIndex<string>(10);

        var value23 = "2-3";
        binIndex.Add(4, 5, value23);
        CollectionAssert.IsEmpty(binIndex.Query(0));
        CollectionAssert.IsEmpty(binIndex.Query(1));
        CollectionAssert.IsEmpty(binIndex.Query(2));
        CollectionAssert.IsEmpty(binIndex.Query(3));
        CollectionAssert.AreEquivalent(new List<string> { value23 }, binIndex.Query(4));
        CollectionAssert.AreEquivalent(new List<string> { value23 }, binIndex.Query(5));
        CollectionAssert.IsEmpty(binIndex.Query(6));
        CollectionAssert.IsEmpty(binIndex.Query(7));
        CollectionAssert.IsEmpty(binIndex.Query(8));
        CollectionAssert.IsEmpty(binIndex.Query(9));
        CollectionAssert.IsEmpty(binIndex.Query(10));

        var value810 = "8-10";
        binIndex.Add(8, 10, value810);
        CollectionAssert.IsEmpty(binIndex.Query(0));
        CollectionAssert.IsEmpty(binIndex.Query(1));
        CollectionAssert.IsEmpty(binIndex.Query(2));
        CollectionAssert.IsEmpty(binIndex.Query(3));
        CollectionAssert.AreEquivalent(new List<string> { value23 }, binIndex.Query(4));
        CollectionAssert.AreEquivalent(new List<string> { value23 }, binIndex.Query(5));
        CollectionAssert.IsEmpty(binIndex.Query(6));
        CollectionAssert.IsEmpty(binIndex.Query(7));
        CollectionAssert.AreEquivalent(new List<string> { value810 }, binIndex.Query(8));
        CollectionAssert.AreEquivalent(new List<string> { value810 }, binIndex.Query(9));
        CollectionAssert.AreEquivalent(new List<string> { value810 }, binIndex.Query(10));

        var value101 = "10-1";
        binIndex.Add(10, 1, value101);
        CollectionAssert.AreEquivalent(new List<string> { value101 }, binIndex.Query(0));
        CollectionAssert.AreEquivalent(new List<string> { value101 }, binIndex.Query(1));
        CollectionAssert.IsEmpty(binIndex.Query(2));
        CollectionAssert.IsEmpty(binIndex.Query(3));
        CollectionAssert.AreEquivalent(new List<string> { value23 }, binIndex.Query(4));
        CollectionAssert.AreEquivalent(new List<string> { value23 }, binIndex.Query(5));
        CollectionAssert.IsEmpty(binIndex.Query(6));
        CollectionAssert.IsEmpty(binIndex.Query(7));
        CollectionAssert.AreEquivalent(new List<string> { value810 }, binIndex.Query(8));
        CollectionAssert.AreEquivalent(new List<string> { value810 }, binIndex.Query(9));
        CollectionAssert.AreEquivalent(new List<string> { value810, value101 }, binIndex.Query(10));
    }
}