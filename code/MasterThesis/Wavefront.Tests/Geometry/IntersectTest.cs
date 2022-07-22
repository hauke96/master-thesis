using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront.Tests.Geometry;

public class IntersectTest
{
    [Test]
    public void Orientation()
    {
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 2)));
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(2, 0)));
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(0, 1), new Coordinate(0, 2)));

        Assert.AreEqual(-1, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 0)));

        Assert.AreEqual(1, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(2, 1)));
    }

    [Test]
    public void IsOnSegment()
    {
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 0)));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(0, 2), new Coordinate(0, 1)));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(1, 1)));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(2, 2)));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(0, 0)));

        Assert.False(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 2)));
        Assert.False(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(-1, -1)));
    }

    [Test]
    public void DoIntersect()
    {
        Assert.True(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(0, 2),
            new Coordinate(2, 0)));
        Assert.True(Intersect.DoIntersect(new Coordinate(0, 2), new Coordinate(2, 0), new Coordinate(0, 0),
            new Coordinate(2, 2)));
        Assert.True(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 1),
            new Coordinate(2, -1)));
        Assert.True(Intersect.DoIntersect(new Coordinate(1, 1), new Coordinate(2, -1), new Coordinate(0, 0),
            new Coordinate(2, 0)));

        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 1),
            new Coordinate(1, 0)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 0),
            new Coordinate(1, 1)));
        Assert.False(Intersect.DoIntersect(new Coordinate(1, 0), new Coordinate(2, 0), new Coordinate(1, 0),
            new Coordinate(2, 0)));
        Assert.False(Intersect.DoIntersect(new Coordinate(1, 0), new Coordinate(2, 0), new Coordinate(0, 0),
            new Coordinate(1, 0)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 0),
            new Coordinate(1, 1)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 1),
            new Coordinate(1, 0)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 0),
            new Coordinate(2, 0)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 1),
            new Coordinate(2, 1)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 2),
            new Coordinate(1, 1)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(0, 1),
            new Coordinate(2, 1)));
        Assert.False(Intersect.DoIntersect(new Coordinate(0, 0), new Coordinate(0, 2), new Coordinate(1, 0),
            new Coordinate(1, 2)));
        Assert.False(Intersect.DoIntersect(new Coordinate(7, 5), new Coordinate(6.5, 3.5), new Coordinate(7, 3),
            new Coordinate(7, 4)));
        Assert.False(Intersect.DoIntersect(new Coordinate(3, 5), new Coordinate(1, 5), new Coordinate(2, 5),
            new Coordinate(2, 10)));
    }
}