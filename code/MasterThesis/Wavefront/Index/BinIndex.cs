using ServiceStack;

namespace Wavefront.Index;

public class BinIndex<T>
{
    private readonly int _exclusiveMaxKey;
    private readonly List<T>[] _index;
    private readonly Dictionary<T, int[]> _areaIndex; // item -> [fromIndex, toIndex]

    public BinIndex(int exclusiveMaxKey, int binSize = 1)
    {
        _exclusiveMaxKey = exclusiveMaxKey;
        var binCount = (int)Math.Ceiling((double)_exclusiveMaxKey / binSize);
        _index = new List<T>[binCount];
        _areaIndex = new Dictionary<T, int[]>();
        for (var i = 0; i < _index.Length; i++)
        {
            _index[i] = new List<T>();
        }
    }

    public void Add(double from, double to, T value)
    {
        if (from < 0 || _exclusiveMaxKey < from)
        {
            throw new ArgumentException($"From-Key must be >=0 and <{_exclusiveMaxKey} but was {from}");
        }

        if (to < 0 || _exclusiveMaxKey < to)
        {
            throw new ArgumentException($"To-Key must be >=0 and <{_exclusiveMaxKey} but was {to}");
        }

        var fromIndex = GetIndexFromKey(from);
        var toIndex = GetIndexFromKey(to);

        _areaIndex.Add(value, new[] { fromIndex, toIndex });

        for (var i = fromIndex; i != toIndex; i = (i + 1) % _index.Length)
        {
            _index[i].Add(value);
        }
    }

    public List<T> Query(double key)
    {
        var index = GetIndexFromKey(key);
        return _index[index];
    }

    public IEnumerable<T> QueryWithin(double from, double to)
    {
        var result = new HashSet<T>();

        if (Double.IsNaN(from) || Double.IsNaN(to))
        {
            return result;
        }

        var queryIndexFrom = GetIndexFromKey(from);
        var queryIndexTo = GetIndexFromKey(to);

        if (double.IsNaN(from))
        {
            Log.I($"from={from}/{queryIndexFrom} to={to}/{queryIndexTo}");
        }

        // Find reference items. If we have an item in query range, then that item should be 
        foreach (var item in _index[queryIndexFrom])
        {
            var area = _areaIndex[item];
            var areaFrom = area[0];
            var areaTo = area[1];
            var completelyWithinQueryRange = false;

            // Check for overlap over _exclusiveMaxKey
            if (areaTo < areaFrom)
            {
                completelyWithinQueryRange = Overlaps(queryIndexFrom, _exclusiveMaxKey - 1, areaFrom, areaTo) &&
                                             Overlaps(0, queryIndexTo, areaFrom, areaTo);
            }
            else
            {
                completelyWithinQueryRange = Overlaps(queryIndexFrom, queryIndexTo, areaFrom, areaTo);
            }

            if (completelyWithinQueryRange)
            {
                result.Add(item);
            }
        }

        if (result.Count == 0)
        {
            return result;
        }

        // Check for overlap over _exclusiveMaxKey
        if (queryIndexTo < queryIndexFrom)
        {
            QueryWithinInternal(queryIndexFrom, _exclusiveMaxKey - 1, result);
            if (result.Count == 0)
            {
                return result;
            }

            QueryWithinInternal(0, queryIndexTo, result);
        }
        else
        {
            QueryWithinInternal(queryIndexFrom, queryIndexTo, result);
        }


        return result;
    }

    /// <summary>
    /// Determines if area b completely overlaps area a.
    /// </summary>
    private static bool Overlaps(int aFrom, int aTo, int bFrom, int bTo)
    {
        return !(aFrom < bFrom && bFrom < aTo) && !(aFrom < bTo && bTo < aTo);
    }

    private void QueryWithinInternal(int indexFrom, int indexTo, HashSet<T> result)
    {
        for (var i = indexFrom; i != indexTo; i++)
        {
            var items = _index[i];
            foreach (var item in items)
            {
                if (!result.Contains(item))
                {
                    result.Remove(item);
                }
            }

            if (result.Count == 0)
            {
                return;
            }
        }
    }

    private int GetIndexFromKey(double key)
    {
        return (int)(key / ((double)_exclusiveMaxKey / _index.Length));
    }
}