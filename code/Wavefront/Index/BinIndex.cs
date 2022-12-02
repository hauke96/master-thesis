using Mars.Common.Core.Collections;

namespace Wavefront.Index;

public class BinIndex<T>
{
    private readonly double _minKey;
    private readonly double _maxKey;
    private readonly double _binsPerKey;
    private readonly bool _isRing;
    private readonly LinkedList<T>[] _index;

    public BinIndex(double minKey, double maxKey, double binsPerKey = 1, bool isRing = false)
    {
        _minKey = minKey;
        _maxKey = maxKey;
        _binsPerKey = binsPerKey;
        _isRing = isRing;
        var binCount = (int)Math.Ceiling((_maxKey - _minKey) * binsPerKey) + 1;
        _index = new LinkedList<T>[binCount];
        for (var i = 0; i < _index.Length; i++)
        {
            _index[i] = new LinkedList<T>();
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

    public ICollection<T> Query(double from, double to)
    {
        var indexFrom = GetIndexFromKey(from);
        var indexTo = GetIndexFromKey(to);
        var result = new HashSet<T>();
        for (var i = indexFrom; i <= indexTo; i++)
        {
            result.AddRange(_index[i]);
        }

        return result;
    }

    public int GetIndexFromKey(double key)
    {
        return (int)((key - _minKey) * _binsPerKey);
    }
}