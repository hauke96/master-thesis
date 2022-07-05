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
        Assert.True(Angle.IsBetween(10, 180, 180));
        Assert.True(Angle.IsBetween(180, 180, 200));

        Assert.False(Angle.IsBetween(0, 90, 360));
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
        Assert.True(Angle.IsBetween(300, 300, 20));
        Assert.True(Angle.IsBetween(300, 20, 20));

        Assert.False(Angle.IsBetween(300, 180, 90));
        // 10, 180, 90
        Assert.False(Angle.IsBetween(-300, 180, 90));
    }

    [Test]
    public void Normalize()
    {
        Assert.AreEqual(90, Angle.Normalize(90));
        Assert.AreEqual(0, Angle.Normalize(0));
        Assert.AreEqual(0, Angle.Normalize(360));
        Assert.AreEqual(0, Angle.Normalize(720));
        Assert.AreEqual(90, Angle.Normalize(-270));
        Assert.AreEqual(180, Angle.Normalize(-180));
    }

    [Test]
    public void Difference()
    {
        Assert.AreEqual(90, Angle.Difference(0, 90));
        Assert.AreEqual(200, Angle.Difference(0, 200));
        Assert.AreEqual(200, Angle.Difference(-180, 20));
        Assert.AreEqual(80, Angle.Difference(300, 20));
        Assert.AreEqual(100, Angle.Difference(300, 400));
    }
}