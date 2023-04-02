using HybridVisibilityGraphRouting.Geometry;
using NUnit.Framework;

namespace HybridVisibilityGraphRouting.Tests.Geometry;

public class AngleTest
{
    [Test]
    public void IsBetweenEqual()
    {
        Assert.True(Angle.IsBetweenEqual(0, 40, 90));
        Assert.True(Angle.IsBetweenEqual(10, 180, 200));
        Assert.True(Angle.IsBetweenEqual(0, 90, 360));
        Assert.True(Angle.IsBetweenEqual(0, 0, 360));
        Assert.True(Angle.IsBetweenEqual(0, 360, 360));
        Assert.True(Angle.IsBetweenEqual(0, 0, 90));
        Assert.True(Angle.IsBetweenEqual(0, 90, 90));
        Assert.True(Angle.IsBetweenEqual(90, 90 - 0.0001, 360));
        Assert.True(Angle.IsBetweenEqual(269.99236701900003, 180.00763299072972, 180.00763298898323));
        Assert.True(Angle.IsBetweenEqual(10, 10, 10));
        Assert.True(Angle.IsBetweenEqual(270, 180, 180));
        Assert.True(Angle.IsBetweenEqual(270, 180, 179.9999));
        Assert.True(Angle.IsBetweenEqual(180, 0, 0));

        Assert.False(Angle.IsBetweenEqual(0, 90, 45));
        Assert.False(Angle.IsBetweenEqual(45, 20, 45));
        Assert.False(Angle.IsBetweenEqual(10, 339, 10));
        Assert.False(Angle.IsBetweenEqual(100, 0, 360));
        Assert.False(Angle.IsBetweenEqual(180, 90, 0));
    }

    [Test]
    public void Normalize()
    {
        Assert.AreEqual(90, Angle.Normalize(90));
        Assert.AreEqual(0, Angle.Normalize(0));
        Assert.AreEqual(359, Angle.Normalize(719));
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
        Assert.AreEqual(359, Angle.StrictNormalize(719));
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
        Assert.True(Angle.AreEqual(0, 0.00009));
        Assert.True(Angle.AreEqual(1.0001, 1));
        Assert.True(Angle.AreEqual(10, 10));
        Assert.True(Angle.AreEqual(10, 10.00001));
        Assert.True(Angle.AreEqual(9.99999, 10.00001));
        Assert.True(Angle.AreEqual(360, 0));
        Assert.True(Angle.AreEqual(359.99999999994, 0));
        Assert.True(Angle.AreEqual(0, 360));
        Assert.True(Angle.AreEqual(0.000001, 360));
        Assert.True(Angle.AreEqual(360, 360));
        Assert.True(Angle.AreEqual(720, 0));
        Assert.True(Angle.AreEqual(0, 720));
        Assert.True(Angle.AreEqual(720, 720));
        Assert.True(Angle.AreEqual(640, 280));

        Assert.False(Angle.AreEqual(0, 0.010000001));
        Assert.False(Angle.AreEqual(0, 1));
        Assert.False(Angle.AreEqual(1.01000001, 1));
        Assert.False(Angle.AreEqual(360, 1));
        Assert.False(Angle.AreEqual(359, 0));
        Assert.False(Angle.AreEqual(720, 10));
        Assert.False(Angle.AreEqual(10, 720));
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

    [Test]
    public void Merge()
    {
        Assert.AreEqual((0, 100), Angle.Merge(0, 50, 50, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 50, 49, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 50, 0, 100));

        Assert.AreEqual((0, 100), Angle.Merge(0, 51, 50, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 50, 100));

        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 0, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 0, 50));
        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 50, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 50, 80));

        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 0, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 50, 0, 100));
        Assert.AreEqual((0, 100), Angle.Merge(50, 100, 0, 100));
        Assert.AreEqual((0, 100), Angle.Merge(50, 80, 0, 100));

        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 10, 90));

        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 0, 0));
        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 100, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 0, 0, 100));
        Assert.AreEqual((0, 100), Angle.Merge(0, 100, 0, 100));

        Assert.AreEqual((270, 90), Angle.Merge(270, 0, 0, 90));
        Assert.AreEqual((270, 90), Angle.Merge(0, 90, 270, 0));

        Assert.AreEqual((270, 90), Angle.Merge(270, 10, 10, 90));
        Assert.AreEqual((270, 90), Angle.Merge(10, 90, 270, 10));
    }
}