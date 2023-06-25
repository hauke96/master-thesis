using NetTopologySuite.Geometries;

namespace HybridVisibilityGraphRouting.Index;

public class RoundedCoordinateIndex<T>
{
    private readonly string _formatStringX;
    private readonly string _formatStringY;
    private readonly Dictionary<string, ISet<T>> _data;

    public RoundedCoordinateIndex(int decimalPlacesX, int decimalPlacesY)
    {
        _formatStringX = "#." + new string('#', decimalPlacesX);
        _formatStringY = "#." + new string('#', decimalPlacesY);
        _data = new Dictionary<string, ISet<T>>();
    }

    public void Add(Coordinate coordinate, T item)
    {
        Add(coordinate.X, coordinate.Y, item);
    }

    public void Add(Mars.Interfaces.Environments.Position position, T item)
    {
        Add(position.X, position.Y, item);
    }

    private void Add(double x, double y, T item)
    {
        var key = GetKeyForCoordinate(x, y);
        if (!_data.ContainsKey(key))
        {
            _data[key] = new HashSet<T>();
        }

        _data[key].Add(item);
    }

    public ICollection<T> Query(Coordinate coordinate)
    {
        return Query(coordinate.X, coordinate.Y);
    }

    public ICollection<T> Query(Mars.Interfaces.Environments.Position position)
    {
        return Query(position.X, position.Y);
    }

    private ICollection<T> Query(double x, double y)
    {
        var key = GetKeyForCoordinate(x, y);
        return _data.ContainsKey(key) ? _data[key] : new HashSet<T>();
    }

    private string GetKeyForCoordinate(double x, double y)
    {
        return x.ToString(_formatStringX) + y.ToString(_formatStringY);
    }
}