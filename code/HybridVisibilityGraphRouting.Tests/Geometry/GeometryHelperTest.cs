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
            CollectionAssert.AreEquivalent(feature.Geometry.Coordinates, result.First(pair => pair.Value.Equals(feature.Geometry)).Value.Coordinates);
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
            CollectionAssert.AreEquivalent(feature.Geometry.Coordinates, result[feature.Geometry].Coordinates);
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

            var triangulatedGeometries = result.Keys.ToList();
            Assert.AreEqual(2, triangulatedGeometries.Count);
            Assert.AreEqual(4, triangulatedGeometries[0].Coordinates.Length);
            Assert.AreEqual(4, triangulatedGeometries[1].Coordinates.Length);
            Assert.AreEqual(feature.Geometry, result[triangulatedGeometries[0]]);
            Assert.AreEqual(feature.Geometry, result[triangulatedGeometries[1]]);
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
            Assert.AreEqual(3, result[lineString1].Coordinates.Length);
            Assert.AreEqual(3, result[lineString2].Coordinates.Length);
            CollectionAssert.AreEquivalent(lineString1.Coordinates, result[lineString1].Coordinates);
            CollectionAssert.AreEquivalent(lineString2.Coordinates, result[lineString2].Coordinates);
        }

        [Test]
        public void TestUnwrapAndTriangulate_TwoRectangles()
        {
            var geometryOuter = new Polygon(
                new LinearRing(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(10, 0),
                    new Coordinate(10, 10),
                    new Coordinate(0, 10),
                    new Coordinate(0, 0)
                })
            );
            var geometryInner = new Polygon(
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
                new MultiPolygon(new[] { geometryOuter, geometryInner }),
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            var triangulatedGeometries = result.Keys.ToList();
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(4, triangulatedGeometries[0].Coordinates.Length);
            Assert.AreEqual(4, triangulatedGeometries[1].Coordinates.Length);
            Assert.AreEqual(4, triangulatedGeometries[2].Coordinates.Length);
            Assert.AreEqual(4, triangulatedGeometries[3].Coordinates.Length);

            var outerTriangles = triangulatedGeometries.Where(g => result[g] == geometryOuter).ToList();
            CollectionAssert.IsSupersetOf(geometryOuter.Coordinates.Distinct(),
                outerTriangles[0].Coordinates.Distinct());
            CollectionAssert.IsSupersetOf(geometryOuter.Coordinates.Distinct(),
                outerTriangles[1].Coordinates.Distinct());

            var innerTriangles = triangulatedGeometries.Where(g => result[g] == geometryInner).ToList();
            CollectionAssert.IsSupersetOf(geometryInner.Coordinates.Distinct(),
                innerTriangles[0].Coordinates.Distinct());
            CollectionAssert.IsSupersetOf(geometryInner.Coordinates.Distinct(),
                innerTriangles[1].Coordinates.Distinct());
        }

        [Test]
        public void TestUnwrapAndTriangulate_PolygonWithInnerRing()
        {
            var feature = new Feature(
                new Polygon(
                    new LinearRing(new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(10, 0),
                        new Coordinate(10, 10),
                        new Coordinate(0, 0)
                    }),
                    new[]
                    {
                        new LinearRing(new[]
                        {
                            new Coordinate(5, 5),
                            new Coordinate(6, 5),
                            new Coordinate(6, 6),
                            new Coordinate(5, 6),
                            new Coordinate(5, 5)
                        })
                    }
                ),
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            var triangulatedGeometries = result.Keys.ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(4, triangulatedGeometries[0].Coordinates.Length);
            Assert.AreEqual(new Polygon(((Polygon)feature.Geometry).Shell), result[triangulatedGeometries[0]]);
        }

        [Test]
        public void TestUnwrapAndTriangulate_NestedMultiGeometries()
        {
            var lineString =
                new LineString(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(1, 0),
                    new Coordinate(1, 1),
                });
            var polygon = new Polygon(
                new LinearRing(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(1, 0),
                    new Coordinate(1, 1),
                    new Coordinate(0, 0),
                })
            );

            var multiLineString = new MultiLineString(new[] { lineString });
            var multiPolygon = new MultiPolygon(new[] { polygon });
            var geometryCollection = new GeometryCollection(new NetTopologySuite.Geometries.Geometry[]
                { multiLineString, multiPolygon, lineString, polygon });

            var feature = new Feature(
                geometryCollection,
                new AttributesTable()
            );

            var result = GeometryHelper.UnwrapAndTriangulate(new List<IFeature> { feature });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(lineString, result[lineString]);
            Assert.AreEqual(polygon, result.First(pair=>pair.Value.Equals(polygon)).Value);
        }
    }

    [Test]
    public void GetEnvelopeOfEdge()
    {
        var edgeData = new EdgeData(0, new Dictionary<string, object>(),
            new[] { new Position(0, 0), new Position(1, 2) }, new[] { 1, 2 }, 1, 2, 3);
        Assert.AreEqual(new Envelope(0, 1, 0, 2), GeometryHelper.GetEnvelope(edgeData.Geometry));

        edgeData = new EdgeData(0, new Dictionary<string, object>(),
            new[] { new Position(0, 0), new Position(1, 2), new Position(5, 0) }, new[] { 1, 2, 3 }, 1, 2, 3);
        Assert.AreEqual(new Envelope(0, 5, 0, 2), GeometryHelper.GetEnvelope(edgeData.Geometry));
    }
}