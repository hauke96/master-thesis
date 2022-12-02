using Mars.Common.Core.Collections;

namespace Wavefront.Index;

public class BinIndex<T>
{
    private readonly double _minKey;
    private readonly double _maxKey;
    private readonly double _binsPerKey;
    private readonly bool _isRing;
    private readonly LinkedList<T>[] _index;
    
    /// <summary>
    /// Stores the items from the previous bin. So it's in a way  _index[i] - _index[i-1].
    /// </summary>
    private readonly LinkedList<T>[] _indexDiff;

    public BinIndex(double minKey, double maxKey, double binsPerKey = 1, bool isRing = false)
    {
        _minKey = minKey;
        _maxKey = maxKey;
        _binsPerKey = binsPerKey;
        _isRing = isRing;
        var binCount = (int)Math.Ceiling((_maxKey - _minKey) * binsPerKey) + 1;
        _index = new LinkedList<T>[binCount];
        _indexDiff = new LinkedList<T>[binCount];
        for (var i = 0; i < _index.Length; i++)
        {
            _index[i] = new LinkedList<T>();
            _indexDiff[i] = new LinkedList<T>();
        }
    }

    public int BinCount => _index.Length;

    public void Add(double from, double to, T value)
    {
        if (from < _minKey || _maxKey < from)
        {
            throw new ArgumentException($"From-Key must be >={_minKey} and <={_maxKey} but was {from}");
        }

        if (to < _minKey || _maxKey < to)
        {
            throw new ArgumentException($"To-Key must be >={_minKey} and <={_maxKey} but was {to}");
        }

        var fromIndex = GetIndexFromKey(from);
        var toIndex = GetIndexFromKey(to);
        
        _indexDiff[fromIndex].AddLast(value);

        if (_isRing)
        {
            for (var i = fromIndex; i != (toIndex + 1) % _index.Length; i = (i + 1) % _index.Length)
            {
                _index[i].AddLast(value);
            }
        }
        else
        {
            for (var i = fromIndex; i != toIndex + 1 && i < _index.Length; i++)
            {
                _index[i].AddLast(value);
            }
        }
    }

    public ICollection<T> Query(double key)
    {
        var index = GetIndexFromKey(key);
        return _index[index];
    }

    public IEnumerable<T> Query(double from, double to)
    {
        var indexFrom = GetIndexFromKey(from);
        var indexTo = GetIndexFromKey(to);
        var result = new List<T>();
        
        result.AddRange(_index[indexFrom]);
        
        for (var i = indexFrom+1; i <= indexTo; i++)
        {
            result.AddRange(_indexDiff[i]);
        }

        if (_isRing)
        {
            // In a ring, the from-bin may contain item that re-appear in later bins. Without a Distinct() call, they
            // would appear twice.
            return result.Distinct();
        }

        return result;
    }

    public int GetIndexFromKey(double key)
    {
        return (int)((key - _minKey) * _binsPerKey);
    }
}