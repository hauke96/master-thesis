using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common.Collections.Graph;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Tests.Geometry;

public class GeometryHelperTest
{
    public class SimpleFeatures : GeometryHelperTest
    {
        [Test]
        public void TestUnwrapAndTriangulate_Triangle()
        {
            var feature = new Feature(
                new Polygon(
                    new LinearRing(new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(1, 0),
                        new Coordinate(1, 1),
                        new Coordinate(0, 0)
                    })
                ),
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            Assert.AreEqual(1, result.Count);
            CollectionAssert.AreEquivalent(feature.Geometry.Coordinates, result[0].Coordinates);
        }

        [Test]
        public void TestUnwrapAndTriangulate_LineString()
        {
            var feature = new Feature(
                new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(1, 0),
                    new Coordinate(1, 1),
                }),
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            Assert.AreEqual(1, result.Count);
            CollectionAssert.AreEquivalent(feature.Geometry.Coordinates, result[0].Coordinates);
        }

        [Test]
        public void TestUnwrapAndTriangulate_Rectangle()
        {
            var feature = new Feature(
                new Polygon(
                    new LinearRing(new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(1, 0),
                        new Coordinate(1, 1),
                        new Coordinate(0, 1),
                        new Coordinate(0, 0)
                    })
                ),
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(4, result[0].Coordinates.Length);
            Assert.AreEqual(4, result[1].Coordinates.Length);
        }
    }

    public class MultiGeometries : GeometryHelperTest
    {
        [Test]
        public void TestUnwrapAndTriangulate_TwoLineStrings()
        {
            var lineString1 =
                new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(1, 0),
                    new Coordinate(1, 1),
                });
            var lineString2 =
                new LineString(new[]
                {
                    new Coordinate(1, 1),
                    new Coordinate(0, 1),
                    new Coordinate(0, 0),
                });

            var feature = new Feature(
                new MultiLineString(new[] { lineString1, lineString2 }),
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(3, result[0].Coordinates.Length);
            Assert.AreEqual(3, result[1].Coordinates.Length);
            CollectionAssert.AreEquivalent(lineString1.Coordinates, result[0].Coordinates);
            CollectionAssert.AreEquivalent(lineString2.Coordinates, result[1].Coordinates);
        }

        [Test]
        public void TestUnwrapAndTriangulate_TwoRectangles()
        {
            var featureOuter = new Polygon(
                new LinearRing(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(10, 0),
                    new Coordinate(10, 10),
                    new Coordinate(0, 10),
                    new Coordinate(0, 0)
                })
            );
            var featureInner = new Polygon(
                new LinearRing(new[]
                {
                    new Coordinate(1, 1),
                    new Coordinate(2, 1),
                    new Coordinate(2, 2),
                    new Coordinate(1, 2),
                    new Coordinate(1, 1)
                })
            );

            var feature = new Feature(
                new MultiPolygon(new[] { featureOuter, featureInner }),
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(4, result[0].Coordinates.Length);
            Assert.AreEqual(4, result[1].Coordinates.Length);
            Assert.AreEqual(4, result[2].Coordinates.Length);
            Assert.AreEqual(4, result[3].Coordinates.Length);
            CollectionAssert.IsSupersetOf(featureOuter.Coordinates.Distinct(), result[0].Coordinates.Distinct());
            CollectionAssert.IsSupersetOf(featureOuter.Coordinates.Distinct(), result[1].Coordinates.Distinct());
            CollectionAssert.IsSupersetOf(featureInner.Coordinates.Distinct(), result[2].Coordinates.Distinct());
            CollectionAssert.IsSupersetOf(featureInner.Coordinates.Distinct(), result[3].Coordinates.Distinct());
        }
    }

    [Test]
    public void GetEnvelopeOfEdge()
    {
        var edgeData = new EdgeData(0, new Dictionary<string, object>(),
            new[] { new Position(0, 0), new Position(1, 2) }, new[] { 1, 2 }, 1, 2, 3);
        Assert.AreEqual(new Envelope(0, 1, 0, 2), GeometryHelper.GetEnvelopeOfEdge(edgeData));

        edgeData = new EdgeData(0, new Dictionary<string, object>(),
            new[] { new Position(0, 0), new Position(1, 2), new Position(5, 0) }, new[] { 1, 2, 3 }, 1, 2, 3);
        Assert.AreEqual(new Envelope(0, 5, 0, 2), GeometryHelper.GetEnvelopeOfEdge(edgeData));
    }
}