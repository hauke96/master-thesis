using HybridVisibilityGraphRouting.Geometry;
using NetTopologySuite.Geometries;
using NUnit.Framework;

namespace HybridVisibilityGraphRouting.Tests.Geometry;

public class IntersectTest
{
    [Test]
    public void Orientation()
    {
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 2)));
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(2, 0)));
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 0)));
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(0, 1), new Coordinate(0, 2)));
        Assert.AreEqual(0, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(0, 2), new Coordinate(0, 1)));

        Assert.AreEqual(-1, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(0, 1), new Coordinate(1, 1)));
        Assert.AreEqual(-1, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 0)));

        Assert.AreEqual(1, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(0, 1), new Coordinate(-1, 1)));
        Assert.AreEqual(1, Intersect.Orientation(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(0, 2)));
    }

    [Test]
    public void IsOnSegment()
    {
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 0), 0));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(0, 2), new Coordinate(0, 1), 0));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(1, 1), 0));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(2, 2), 0));
        Assert.True(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(0, 0), 0));

        Assert.False(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 2), 0));
        Assert.False(Intersect.IsOnSegment(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(-1, -1), 0));
    }

    [Test]
    public void DoIntersectOrTouch()
    {
        // Intersecting
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(0, 2),
            new Coordinate(2, 0)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 2), new Coordinate(2, 0), new Coordinate(0, 0),
            new Coordinate(2, 2)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 1),
            new Coordinate(2, -1)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(1, 1), new Coordinate(2, -1), new Coordinate(0, 0),
            new Coordinate(2, 0)));

        // Touching
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 1),
            new Coordinate(1, 0)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 0),
            new Coordinate(1, 1)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(1, 0), new Coordinate(2, 0), new Coordinate(1, 0),
            new Coordinate(2, 0)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(1, 0), new Coordinate(2, 0), new Coordinate(0, 0),
            new Coordinate(1, 0)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 0),
            new Coordinate(1, 1)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 1),
            new Coordinate(1, 0)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 0),
            new Coordinate(2, 0)));
        Assert.True(Intersect.DoIntersectOrTouch(new Coordinate(3, 5), new Coordinate(1, 5), new Coordinate(2, 5),
            new Coordinate(2, 10)));

        // Not intersecting/touching
        Assert.False(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 1),
            new Coordinate(2, 1)));
        Assert.False(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(1, 2),
            new Coordinate(1, 1)));
        Assert.False(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(2, 0), new Coordinate(0, 1),
            new Coordinate(2, 1)));
        Assert.False(Intersect.DoIntersectOrTouch(new Coordinate(0, 0), new Coordinate(0, 2), new Coordinate(1, 0),
            new Coordinate(1, 2)));
        Assert.False(Intersect.DoIntersectOrTouch(new Coordinate(2, 1), new Coordinate(3, 2), new Coordinate(3, 1),
            new Coordinate(3, 0)));
        Assert.False(Intersect.DoIntersectOrTouch(new Coordinate(3, 1), new Coordinate(3, 0), new Coordinate(2, 1),
            new Coordinate(3, 2)));
    }
}