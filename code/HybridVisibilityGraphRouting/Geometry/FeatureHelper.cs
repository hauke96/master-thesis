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
                var coordinates = f.Geometry.Coordinates;
                for (var i = 0; i < coordinates.Length - 1; i++)
                {
                    features.Add(new Feature(new LineString(new[] { coordinates[i], coordinates[i + 1] }),
                        f.Attributes));
                }

                return features;
            })
            .SelectMany(x => x)
            .ToList();
    }

    /*
     * Syntax for the filter expression strings. The regex is used to exclude features.
     * 
     *   <key>
     *   <key>!=<regex>
     *
     *  Examples:
     *   barrier
     *   barrier!=^(kerb|bollard|*gate|cycle_barrier|no)$
     */
    /// <summary>
    /// The "filterExpressionStrings" can contain simple keys or regular expressions to exclude features.
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