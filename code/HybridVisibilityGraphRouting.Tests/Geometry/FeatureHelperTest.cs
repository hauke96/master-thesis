using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NUnit.Framework;

namespace HybridVisibilityGraphRouting.Tests.Geometry;

public class FeatureHelperTest
{
    [Test]
    public void SplitFeaturesToSegments()
    {
        var features = new List<IFeature>();
        features.Add(new Feature(
            new LineString(
                new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(1, 0)
                }
            ),
            new AttributesTable()
        ));
        features.Add(new Feature(
            new LineString(
                new[]
                {
                    new Coordinate(0, 1),
                    new Coordinate(1, 1),
                    new Coordinate(2, 1)
                }
            ),
            new AttributesTable()
        ));
        features.Add(new Feature(
            new Polygon(
                new LinearRing(
                    new[]
                    {
                        new Coordinate(0, 2),
                        new Coordinate(1, 2),
                        new Coordinate(1, 3),
                        new Coordinate(0, 2)
                    }
                )
            ),
            new AttributesTable()
        ));

        var splitFeatures = FeatureHelper.SplitFeaturesToSegments(features);

        Assert.AreEqual(6, splitFeatures.Count);

        // feature[0]
        Assert.AreEqual(new LineString(
            new[]
            {
                new Coordinate(0, 0),
                new Coordinate(1, 0)
            }
        ), splitFeatures[0].Geometry);

        // feature[1]
        Assert.AreEqual(new LineString(
            new[]
            {
                new Coordinate(0, 1),
                new Coordinate(1, 1)
            }
        ), splitFeatures[1].Geometry);
        Assert.AreEqual(new LineString(
            new[]
            {
                new Coordinate(1, 1),
                new Coordinate(2, 1)
            }
        ), splitFeatures[2].Geometry);

        // feature[2]
        Assert.AreEqual(new LineString(
            new[]
            {
                new Coordinate(0, 2),
                new Coordinate(1, 2)
            }
        ), splitFeatures[3].Geometry);
        Assert.AreEqual(new LineString(
            new[]
            {
                new Coordinate(1, 2),
                new Coordinate(1, 3)
            }
        ), splitFeatures[4].Geometry);
        Assert.AreEqual(new LineString(
            new[]
            {
                new Coordinate(1, 3),
                new Coordinate(0, 2)
            }
        ), splitFeatures[5].Geometry);
    }

    [Test]
    public void FilterFeaturesByExpressions()
    {
        var features = new List<IFeature>();
        features.Add(new Feature(
                new LineString(
                    new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(1, 0)
                    }
                ),
                new AttributesTable(
                    new Dictionary<string, object>
                    {
                        { "highway", "road" }
                    }
                )
            )
        );
        features.Add(new Feature(
                new LineString(
                    new[]
                    {
                        new Coordinate(1, 0),
                        new Coordinate(2, 0)
                    }
                ),
                new AttributesTable(
                    new Dictionary<string, object>
                    {
                        { "foo", "bar" }
                    }
                )
            )
        );

        // Act & Assert
        List<IFeature> filteredFeatures;

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "highway").ToList();
        Assert.AreEqual(1, filteredFeatures.Count);
        Assert.AreEqual(features[0], filteredFeatures[0]);

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "foo").ToList();
        Assert.AreEqual(1, filteredFeatures.Count);
        Assert.AreEqual(features[1], filteredFeatures[0]);

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "something-else").ToList();
        Assert.AreEqual(0, filteredFeatures.Count);
    }

    [Test]
    public void FilterFeaturesByExpressions_Regex()
    {
        var features = new List<IFeature>();
        features.Add(new Feature(
                new LineString(
                    new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(1, 0)
                    }
                ),
                new AttributesTable(
                    new Dictionary<string, object>
                    {
                        { "highway", "road" }
                    }
                )
            )
        );
        features.Add(new Feature(
                new LineString(
                    new[]
                    {
                        new Coordinate(1, 0),
                        new Coordinate(2, 0)
                    }
                ),
                new AttributesTable(
                    new Dictionary<string, object>
                    {
                        { "foo", "bar" }
                    }
                )
            )
        );

        // Act & Assert
        List<IFeature> filteredFeatures;

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "highway!=.o.*").ToList();
        Assert.AreEqual(0, filteredFeatures.Count);

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "highway!=^(motorway|road|cycleway)$")
            .ToList();
        Assert.AreEqual(0, filteredFeatures.Count);

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "highway!=^(motorway|cycleway)$")
            .ToList();
        Assert.AreEqual(1, filteredFeatures.Count);
        Assert.AreEqual(features[0], filteredFeatures[0]);

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "foo!=bar").ToList();
        Assert.AreEqual(0, filteredFeatures.Count);

        filteredFeatures = FeatureHelper.FilterFeaturesByExpressions(features, "something-else!=.*").ToList();
        Assert.AreEqual(0, filteredFeatures.Count);
    }
}