using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;

namespace HybridVisibilityGraphRouting.IO;

public class CoordinateZMConverter : CoordinateConverter
{
    private readonly PrecisionModel _precisionModel;

    public CoordinateZMConverter()
        : this(GeometryFactory.Floating.PrecisionModel, 2)
    {
    }

    public CoordinateZMConverter(PrecisionModel precisionModel, int dimension)
        : base(precisionModel, dimension)
    {
        _precisionModel = precisionModel;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        switch (value)
        {
            case null:
                writer.WriteToken(JsonToken.Null);
                break;
            case IEnumerable<IEnumerable<IEnumerable<Coordinate>>>:
                base.WriteJson(writer, value, serializer);
                break;

            case IEnumerable<IEnumerable<Coordinate>>:
                base.WriteJson(writer, value, serializer);
                break;

            case IEnumerable<Coordinate> sequence:
                WriteJsonCoordinates(writer, sequence);
                break;

            case Coordinate coordinate:
                WriteJsonCoordinate(writer, coordinate);
                break;
        }
    }

    private void WriteJsonCoordinates(JsonWriter writer, IEnumerable<Coordinate> coordinates)
    {
        writer.WriteStartArray();
        foreach (var coordinate in coordinates)
        {
            WriteJsonCoordinate(writer, coordinate);
        }

        writer.WriteEndArray();
    }

    private new void WriteJsonCoordinate(JsonWriter writer, Coordinate coordinate)
    {
        writer.WriteStartArray();

        var x = _precisionModel.MakePrecise(coordinate.X);
        var y = _precisionModel.MakePrecise(coordinate.Y);
        var z = 0d;
        var m = 0d;
        if (coordinate is CoordinateZ)
        {
            z = _precisionModel.MakePrecise(coordinate.Z);
        }

        if (coordinate is CoordinateM || coordinate is CoordinateZM)
        {
            m = _precisionModel.MakePrecise(coordinate.M);
        }

        writer.WriteValue(x);
        writer.WriteValue(y);
        writer.WriteValue(z);
        writer.WriteValue(m);

        writer.WriteEndArray();
    }
}