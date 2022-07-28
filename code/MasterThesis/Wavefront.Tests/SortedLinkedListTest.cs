using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Wavefront.Tests;

public class SortedLinkedListTest
{
    private SortedLinkedList<String> list;

    [SetUp]
    public void setup()
    {
        list = new SortedLinkedList<String>(8);
    }

    [Test]
    public void Add()
    {
        Assert.AreEqual(0, list.Count);
        Assert.IsNull(list.First?.Value.Value);

        list.Add("5.00001", 5.00001, 5.00001);
        Assert.IsTrue(list.Contains("5.00001"));
        Assert.AreEqual("5.00001", list.First.Value.Value, list.First.Value.Value);

        list.Add("1", 1, 1);
        Assert.IsTrue(list.Contains("1"));
        Assert.AreEqual("1", list.First.Value.Value, list.First.Value.Value);

        list.Add("5.000005", 5.000005, 5.000005);
        Assert.IsTrue(list.Contains("5.000005"));
        Assert.AreEqual("1", list.First.Value.Value, list.First.Value.Value);

        list.Add("5", 5, 5);
        Assert.IsTrue(list.Contains("5"));
        Assert.AreEqual("1", list.First.Value.Value, list.First.Value.Value);

        list.Add("4.9", 4.9, 4.9);
        Assert.IsTrue(list.Contains("4.9"));
        Assert.AreEqual("1", list.First.Value.Value, list.First.Value.Value);
    }

    [Test]
    public void AddElementTwice()
    {
        Assert.AreEqual(0, list.Count);

        list.Add("5", 5, 5);
        Assert.IsTrue(list.Contains("5"));

        list.Add("5", 5, 5);
        Assert.IsTrue(list.Contains("5"));
        Assert.AreEqual("5", list.First.Value.Value, list.First.Value.Value);
    }

    [Test]
    public void SortedWithOnlyOneBucketInIndex()
    {
        // Keys are all way to high for index -> should still work
        list = new SortedLinkedList<String>();

        list.Add("4.9", 4.9, 4.9);
        list.Add("5.00001", 5.00001, 5.00001);
        list.Add("1", 1, 1);
        list.Add("5", 5, 5);
        Assert.AreEqual("1", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsTrue(list.Contains("4.9"));
        Assert.IsTrue(list.Contains("5"));
        Assert.IsTrue(list.Contains("5.00001"));
        Assert.AreEqual("4.9", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsFalse(list.Contains("4.9"));
        Assert.IsTrue(list.Contains("5"));
        Assert.IsTrue(list.Contains("5.00001"));
        Assert.AreEqual("5", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsFalse(list.Contains("4.9"));
        Assert.IsFalse(list.Contains("5"));
        Assert.IsTrue(list.Contains("5.00001"));
        Assert.AreEqual("5.00001", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsFalse(list.Contains("4.9"));
        Assert.IsFalse(list.Contains("5"));
        Assert.IsFalse(list.Contains("5.00001"));
        Assert.AreEqual(0, list.Count);
    }

    [Test]
    public void RemoveFirst()
    {
        list.Add("4.9", 4.9, 4.9);
        list.Add("5.00001", 5.00001, 5.00001);
        list.Add("1", 1, 1);
        list.Add("5", 5, 5);
        Assert.AreEqual("1", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsTrue(list.Contains("4.9"));
        Assert.IsTrue(list.Contains("5"));
        Assert.IsTrue(list.Contains("5.00001"));
        Assert.AreEqual("4.9", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsFalse(list.Contains("4.9"));
        Assert.IsTrue(list.Contains("5"));
        Assert.IsTrue(list.Contains("5.00001"));
        Assert.AreEqual("5", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsFalse(list.Contains("4.9"));
        Assert.IsFalse(list.Contains("5"));
        Assert.IsTrue(list.Contains("5.00001"));
        Assert.AreEqual("5.00001", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("1"));
        Assert.IsFalse(list.Contains("4.9"));
        Assert.IsFalse(list.Contains("5"));
        Assert.IsFalse(list.Contains("5.00001"));
        Assert.AreEqual(0, list.Count);
    }

    [Test]
    public void RemoveFirst_ElementTwiceInserted()
    {
        list.Add("5", 5, 5);
        list.Add("5", 5, 5);
        Assert.AreEqual("5", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.AreEqual("5", list.First.Value.Value, list.First.Value.Value);

        list.RemoveFirst();
        Assert.IsFalse(list.Contains("5"));
        Assert.AreEqual(0, list.Count);
    }

    [Test]
    public void LoopOverElements()
    {
        var elements = new List<int>() { 5, 2, 4, 3, 1 };
        elements.ForEach(e => list.Add(e.ToString(), e, e));

        var itemsFromList = new List<String>();
        foreach (var listItem in list)
        {
            itemsFromList.Add(listItem);
        }

        Assert.AreEqual(elements[4].ToString(), itemsFromList[0]);
        Assert.AreEqual(elements[3].ToString(), itemsFromList[2]);
        Assert.AreEqual(elements[2].ToString(), itemsFromList[3]);
        Assert.AreEqual(elements[1].ToString(), itemsFromList[1]);
        Assert.AreEqual(elements[0].ToString(), itemsFromList[4]);
    }
}