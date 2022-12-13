using System.Globalization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Operation.Overlay.Snap;
using Newtonsoft.Json;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;

namespace DatasetCreator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        
        if (args.Length != 6)
        {
            Console.WriteLine(@$"ERROR: 6 arguments required but {args.Length} found.

Usage: {{minX}} {{minY}} {{maxX}} {{maxY}} {{horiz-nu}} {{vert-nu}}

Parameters:
  minX      Minimum x-coordinate of the output area (double value).
  minY      Minimum y-coordinate of the output area (double value).
  maxX      Maximum x-coordinate of the output area (double value).
  maxY      Maximum y-coordinate of the output area (double value).
  horiz-nu  Number of pattern repetitions in horizontal direction (integer value). 
  vert-nu   Number of pattern repetitions in vertical direction (integer value).");

            Environment.Exit(1);
        }

        var minX = double.Parse(args[0]);
        var minY = double.Parse(args[1]);
        var maxX = double.Parse(args[2]);
        var maxY = double.Parse(args[3]);
        var patternRepetitionsX = int.Parse(args[4]);
        var patternRepetitionsY = int.Parse(args[5]);
        var widthPerPattern = (maxX - minX) / patternRepetitionsX;
        var heightPerPattern = (maxY - minY) / patternRepetitionsY;

        var geometryCollection =
            new WKTFileReader("Resources/pattern.wkt", new WKTReader()).Read()[0] as GeometryCollection;

        // Scale to geometry with envelope of [0, 0, 1, 1] to be easily rescalable and translatable.
        var e = geometryCollection.EnvelopeInternal;
        var originalWidth = e.MaxX - e.MinX;
        var originalHeight = e.MaxY - e.MinY;
        geometryCollection = AffineTransformation
            .TranslationInstance(-e.MinX, -e.MinY)
            .Scale(1 / originalWidth, 1 / originalHeight)
            .Transform(geometryCollection) as GeometryCollection;

        await writeAsFeaturesToFile(geometryCollection.Geometries.ToList(), "./translated-scaled-pattern.geojson");

        // Scale and translate the pattern and do that for each requested pattern.
        var resultGeometries = new List<Geometry>();
        for (var x = 0; x < patternRepetitionsX; x++)
        {
            for (var y = 0; y < patternRepetitionsY; y++)
            {
                var translatedGeometries = AffineTransformation.ScaleInstance(widthPerPattern, heightPerPattern)
                    .Translate(minX + x * widthPerPattern, minY + y * heightPerPattern)
                    .Transform(geometryCollection);
                resultGeometries.AddRange((translatedGeometries as GeometryCollection).Geometries);
            }
        }

        // Merge vertices and snap line ends to other lines.
        var allGeometries = new GeometryCollection(resultGeometries.ToArray());
        var unsnappedGeometries = new List<Geometry>(resultGeometries);
        var snappedGeometries = new List<Geometry>();
        foreach (var unsnappedGeometry in unsnappedGeometries)
        {
            var snappedGeometry = new GeometrySnapper(unsnappedGeometry).SnapTo(allGeometries, 0.00001);
            snappedGeometry = new GeometrySnapper(snappedGeometry).SnapToSelf(0.00001, true);
            snappedGeometries.Add(snappedGeometry);
        }

        var outputGeojsonFile = "./obstacles.geojson";
        await writeAsFeaturesToFile(snappedGeometries, outputGeojsonFile);
    }

    private static async Task writeAsFeaturesToFile(List<Geometry> snappedGeometries, string outputGeojsonFile)
    {
        // MARS somehow needs features, so let's turn the whole thing into a feature collection.
        var features = snappedGeometries
            .Map(g =>
                new Feature(g, new AttributesTable(new Dictionary<string, object> { { "obstacle", "yes" } })));

        var result = new FeatureCollection();
        features.Each(f => result.Add(f));

        // To GeoJSON:
        var serializer = GeoJsonSerializer.Create();
        await using var stringWriter = new StringWriter();
        using var jsonWriter = new JsonTextWriter(stringWriter);

        serializer.Serialize(jsonWriter, result);
        var geoJson = stringWriter.ToString();

        await File.WriteAllTextAsync(outputGeojsonFile, geoJson);
    }
}