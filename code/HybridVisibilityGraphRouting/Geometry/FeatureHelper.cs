using System.Text.RegularExpressions;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;

namespace HybridVisibilityGraphRouting.Geometry;

public class FeatureHelper
{
    /// <summary>
    /// Determines a list of all segments from the given features. That means, each of the returned line strings has
    /// exactly two coordinates.
    /// </summary>
    public static List<Feature> SplitFeaturesToSegments(IEnumerable<IFeature> roadFeatures)
    {
        return roadFeatures
            // Turn each highway edge into a list of line strings with only two coordinates. This makes it easier to
            // split them at intersection points with visibility edges.
            .Map(f =>
            {
                var features = new List<Feature>();
                switch (f.Geometry.OgcGeometryType)
                {
                    case OgcGeometryType.LineString:
                        features.AddRange(ToSegmentFeatures(f.Geometry.Coordinates, f.Attributes));
                        break;
                    case OgcGeometryType.Polygon:
                    {
                        var p = (Polygon)f.Geometry;
                        features.AddRange(ToSegmentFeatures(p.ExteriorRing.Coordinates, f.Attributes));
                        p.InteriorRings.Each(ring =>
                            features.AddRange(ToSegmentFeatures(ring.Coordinates, f.Attributes))
                        );
                        break;
                    }
                    default:
                        throw new Exception($"Unsupported geometry type {f.Geometry.OgcGeometryType}");
                }

                return features;
            })
            .SelectMany(x => x)
            .ToList();
    }

    private static List<Feature> ToSegmentFeatures(Coordinate[] coordinates, IAttributesTable attributesTable)
    {
        var newFeatures = new List<Feature>();
        for (var i = 0; i < coordinates.Length - 1; i++)
        {
            newFeatures.Add(new Feature(new LineString(new[] { coordinates[i], coordinates[i + 1] }),
                attributesTable));
        }

        return newFeatures;
    }

    /// <summary>
    /// Gets all features based on the given filter expression. The "filterExpressionStrings" can contain simple keys or
    /// regular expressions to exclude features.
    ///
    /// Syntax:
    /// <ul>
    ///   <li>{key}</li>
    ///   <li>{key}!={regex}</li>
    /// </ul>
    /// 
    /// Examples:
    /// <ul>
    ///   <li>barrier</li>
    ///   <li>barrier!=^(kerb|bollard|*gate|cycle_barrier|no)$</li>
    /// </ul>
    /// </summary>
    /// <param name="features"></param>
    /// <param name="filterExpressionStrings"></param>
    /// <returns></returns>
    public static IEnumerable<IFeature> FilterFeaturesByExpressions(IEnumerable<IFeature> features,
        params string[] filterExpressionStrings)
    {
        var filterExpressions = filterExpressionStrings
            .ToDictionary(
                expr => expr.Split("!=")[0],
                expr => new Regex(expr.Split("!=")[1..].Join("!="))
            );

        return features
            .Where(f =>
            {
                return f.Attributes.GetNames().Any(name =>
                {
                    var lowerName = name.ToLower();
                    return filterExpressions.Any(pair =>
                        pair.Key.Equals(lowerName) && (pair.Value.ToString().IsEmpty() ||
                                                       !pair.Value.IsMatch(f.Attributes[name].ToString() ?? "")));
                });
            });
    }
}