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

    public static IEnumerable<IFeature> FilterFeaturesByKeys(IEnumerable<IFeature> features, params string[] wantedKeys)
    {
        return features
            .Where(f =>
            {
                return f.Attributes.GetNames().Any(name =>
                {
                    var lowerName = name.ToLower();
                    return wantedKeys.Any(key => key.Equals(lowerName));
                });
            });
    }
}