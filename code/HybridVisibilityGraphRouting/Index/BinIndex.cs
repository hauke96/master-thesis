namespace HybridVisibilityGraphRouting.Index;

public class BinIndex<T>
{
    private readonly int _maxKey;
    private readonly LinkedList<T>[] _index;

    public BinIndex(int maxKey, int binSize = 1)
    {
        _maxKey = maxKey;
        var binCount = (int)Math.Floor((double)_maxKey / binSize) + 1;
        _index = new LinkedList<T>[binCount];
        for (var i = 0; i < _index.Length; i++)
        {
            _index[i] = new LinkedList<T>();
        }
    }

    public void Add(double from, double to, T value)
    {
        if (from < 0 || _maxKey < from)
        {
            throw new ArgumentException($"From-Key must be >=0 and <={_maxKey} but was {from}");
        }

        if (to < 0 || _maxKey < to)
        {
            throw new ArgumentException($"To-Key must be >=0 and <={_maxKey} but was {to}");
        }

        var fromIndex = GetIndexFromKey(from);
        var toIndex = GetIndexFromKey(to);

        for (var i = fromIndex; i != (toIndex + 1) % _index.Length; i = (i + 1) % _index.Length)
        {
            _index[i].AddLast(value);
        }
    }

    public LinkedList<T> Query(double key)
    {
        if (key < 0 || _maxKey < key)
        {
            throw new ArgumentException($"Key must be >=0 and <={_maxKey} but was {key}");
        }
        
        var index = GetIndexFromKey(key);
        if (index >= _index.Length)
        {
            Console.WriteLine($"OK {key} -> {index}");
        }

        return _index[index];
    }

    private int GetIndexFromKey(double key)
    {
        return (int)(key / ((double)_maxKey / (_index.Length - 1)));
    }
}