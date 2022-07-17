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
    public void StrictNormalize()
    {
        Assert.AreEqual(90, Angle.StrictNormalize(90));
        Assert.AreEqual(0, Angle.StrictNormalize(0));
        Assert.AreEqual(0, Angle.StrictNormalize(720));
        Assert.AreEqual(90, Angle.StrictNormalize(-270));
        Assert.AreEqual(180, Angle.StrictNormalize(-180));
        Assert.AreEqual(0, Angle.StrictNormalize(360));
        Assert.AreEqual(1, Angle.StrictNormalize(361));
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
    public void AreEqual()
    {
        Assert.True(Angle.AreEqual(0, 0));
        Assert.True(Angle.AreEqual(0, 0.0001));
        Assert.True(Angle.AreEqual(1.0001, 1));
        Assert.True(Angle.AreEqual(10, 10));
        Assert.True(Angle.AreEqual(10, 10.00001));
        Assert.True(Angle.AreEqual(9.99999, 10.00001));
        Assert.True(Angle.AreEqual(360, 0));
        Assert.True(Angle.AreEqual(0, 360));
        Assert.True(Angle.AreEqual(1000, 280));

        Assert.False(Angle.AreEqual(0, 0.010000001));
        Assert.False(Angle.AreEqual(0, 1));
        Assert.False(Angle.AreEqual(1.01000001, 1));
        Assert.False(Angle.AreEqual(360, 1));
        Assert.False(Angle.AreEqual(359, 0));
    }

    [Test]
    public void GreaterEqual()
    {
        Assert.True(Angle.GreaterEqual(1, 0));
        Assert.True(Angle.GreaterEqual(1, 0.9899999999));
        Assert.True(Angle.GreaterEqual(1, 1.0001));
        Assert.True(Angle.GreaterEqual(360, 0));
        Assert.True(Angle.GreaterEqual(0, 360));
        Assert.True(Angle.GreaterEqual(1, 360.1));

        Assert.False(Angle.GreaterEqual(1, 1.01000001));
        Assert.False(Angle.GreaterEqual(1, 359.9));
        Assert.False(Angle.GreaterEqual(1, 360));
        Assert.False(Angle.GreaterEqual(359.9, 360));
    }

    [Test]
    public void LowerEqual()
    {
        Assert.True(Angle.LowerEqual(0, 1));
        Assert.True(Angle.LowerEqual(360, 0));
        Assert.True(Angle.LowerEqual(0, 360));
        Assert.True(Angle.LowerEqual(1.0001, 1));
        Assert.True(Angle.LowerEqual(360.1, 1));
        Assert.True(Angle.LowerEqual(359.9, 360));

        Assert.False(Angle.LowerEqual(359.9, 1));
        Assert.False(Angle.LowerEqual(360, 1));
        Assert.False(Angle.LowerEqual(1.01000001, 1));
    }
}