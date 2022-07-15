using NUnit.Framework;
using Wavefront.Geometry;

namespace Wavefront.Tests.Geometry;

public class AngleTest
{
    [Test]
    public void IsBetween_noZeroDegreeOverlap()
    {
        Assert.True(Angle.IsBetween(0, 40, 90));
        Assert.True(Angle.IsBetween(10, 180, 200));
        Assert.True(Angle.IsBetween(0, 90, 360));

        Assert.False(Angle.IsBetween(10, 180, 180));
        Assert.False(Angle.IsBetween(180, 180, 200));
        Assert.False(Angle.IsBetween(0, 90, 361));
        Assert.False(Angle.IsBetween(-1, 90, 360));
        Assert.False(Angle.IsBetween(0, 90, 45));
    }

    [Test]
    public void IsBetween_noZeroDegreeOverlap_outside360DegreeArea()
    {
        // 0, 40, 90
        Assert.True(Angle.IsBetween(0, 40, 450));
        // 180, 200, 250
        Assert.True(Angle.IsBetween(-180, 200, 250));
        // 10, 60, 160
        Assert.True(Angle.IsBetween(-350, -300, -200));

        // 60, 10, 160
        Assert.False(Angle.IsBetween(-300 - 360 - 360, -350, -200));
    }

    [Test]
    public void IsBetween_withZeroDegreeOverlap()
    {
        Assert.True(Angle.IsBetween(300, 40, 90));
        Assert.True(Angle.IsBetween(-60, 40, 90));
        Assert.True(Angle.IsBetween(300, 350, 20));

        Assert.False(Angle.IsBetween(300, 20, 20));
        Assert.False(Angle.IsBetween(300, 300, 20));
        Assert.False(Angle.IsBetween(300, 180, 90));
        // 10, 180, 90
        Assert.False(Angle.IsBetween(-300, 180, 90));
    }

    [Test]
    public void IsEnclosedBy()
    {
        Assert.True(Angle.IsEnclosedBy(0, 10, 20));
        Assert.True(Angle.IsEnclosedBy(20, 10, 0));
        Assert.True(Angle.IsEnclosedBy(-10, 10, 20));
        Assert.True(Angle.IsEnclosedBy(20, 10, -10));
        Assert.True(Angle.IsEnclosedBy(350, 10, 380));
        Assert.True(Angle.IsEnclosedBy(380, 10, 350));
        
        Assert.False(Angle.IsEnclosedBy(10, 10, 20));
        Assert.False(Angle.IsEnclosedBy(0, 20, 20));
        Assert.False(Angle.IsEnclosedBy(0, 90, 200));
        Assert.False(Angle.IsEnclosedBy(200, 90, 0));
    }

    [Test]
    public void Normalize()
    {
        Assert.AreEqual(90, Angle.Normalize(90));
        Assert.AreEqual(0, Angle.Normalize(0));
        Assert.AreEqual(0, Angle.Normalize(720));
        Assert.AreEqual(90, Angle.Normalize(-270));
        Assert.AreEqual(180, Angle.Normalize(-180));
        Assert.AreEqual(360, Angle.Normalize(360));
        Assert.AreEqual(1, Angle.Normalize(361));
    }

    [Test]
    public void Difference()
    {
        Assert.AreEqual(90, Angle.Difference(0, 90));
        Assert.AreEqual(200, Angle.Difference(0, 200));
        Assert.AreEqual(200, Angle.Difference(-180, 20));
        Assert.AreEqual(80, Angle.Difference(300, 20));
        Assert.AreEqual(100, Angle.Difference(300, 400));
        Assert.AreEqual(0, Angle.Difference(100, 100));
    }

    [Test]
    public void GetEnclosingAngle()
    {
        double from;
        double to;

        Angle.GetEnclosingAngles(10, 200, out from, out to);
        Assert.AreEqual(200, from);
        Assert.AreEqual(10, to);

        Angle.GetEnclosingAngles(10, 90, out from, out to);
        Assert.AreEqual(10, from);
        Assert.AreEqual(90, to);

        Angle.GetEnclosingAngles(350, 90, out from, out to);
        Assert.AreEqual(350, from);
        Assert.AreEqual(90, to);
    }

    [Test]
    public void Overlap()
    {
        Assert.True(Angle.Overlap(0, 90, 40, 100));
        Assert.True(Angle.Overlap(0, 90, 10, 80));
        Assert.True(Angle.Overlap(40, 100, 0, 90));
        Assert.True(Angle.Overlap(10, 80, 0, 90));
        Assert.True(Angle.Overlap(350, 90, 40, 100));
        Assert.True(Angle.Overlap(90, 180, 160, 10));
        Assert.True(Angle.Overlap(350, 180, 160, 10));

        Assert.False(Angle.Overlap(0, 90, 100, 120));
        Assert.False(Angle.Overlap(100, 120, 0, 90));
        Assert.False(Angle.Overlap(350, 90, 100, 120));
        Assert.False(Angle.Overlap(40, 90, 100, 10));
        Assert.False(Angle.Overlap(10, 10, 20, 30));
        Assert.False(Angle.Overlap(10, 20, 30, 30));
    }
}