using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;

namespace Wavefront;

public class GeometryZMConverter : GeometryConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (!(value is NetTopologySuite.Geometries.Geometry geom))
        {
            writer.WriteNull();
            return;
        }


        bool writeCoordinateData = serializer.NullValueHandling == NullValueHandling.Include || !geom.IsEmpty;

        switch (geom)
        {
            case LineString lineString:
                writer.WriteStartObject();
                writer.WritePropertyName("type");
                writer.WriteValue(nameof(GeoJsonObjectType.LineString));

                if (writeCoordinateData)
                {
                    writer.WritePropertyName("coordinates");
                    serializer.Serialize(writer, GetCoordinatesFromLineString(lineString));
                }

                break;
            default:
                base.WriteJson(writer, value, serializer);
                return;
        }

        writer.WriteEndObject();

        IEnumerable<Coordinate> GetCoordinatesFromLineString(LineString lineString)
        {
            var seq = lineString.CoordinateSequence;
            for (int i = 0, cnt = seq.Count; i < cnt; i++)
            {
                yield return seq.GetCoordinate(i);
            }
        }
    }
}