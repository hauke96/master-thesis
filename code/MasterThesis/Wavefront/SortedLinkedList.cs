using System.Collections;

namespace Wavefront;

public class SortedLinkedList<T> : ICollection<T>
{
    private readonly Dictionary<int, LinkedListNode<SortedLinkedListNode<T>>> _index;
    private readonly int _accuracy;
    private readonly LinkedList<SortedLinkedListNode<T>> _list;

    public int Count => _list.Count;
    public bool IsReadOnly => false;
    public LinkedListNode<SortedLinkedListNode<T>>? First => Count > 0 ? _list.First : null;

    public SortedLinkedList(int accuracy = 2)
    {
        _list = new LinkedList<SortedLinkedListNode<T>>();
        _index = new Dictionary<int, LinkedListNode<SortedLinkedListNode<T>>>();
        _accuracy = accuracy;
    }

    public void Add(T item)
    {
        Add(item, 0);
    }

    public LinkedListNode<SortedLinkedListNode<T>> Add(T value, double sortingKey)
    {
        Log.I($"Add item {value} with key {sortingKey}");
        var key = GetKey(sortingKey);
        if (!_index.ContainsKey(key))
        {
            return AddItemToBin(value, sortingKey, _list.First, key);
        }

        return AddItemToBin(value, sortingKey, _index[key], key);
    }

    public void RemoveFirst()
    {
        if (First != null)
        {
            Remove(First);
        }
    }

    public void Remove(LinkedListNode<SortedLinkedListNode<T>> node)
    {
        var key = GetKey(node.Value.Key);
        if (!_index.ContainsKey(key) || _index.ContainsKey(key) && !_index[key].Equals(node))
        {
            _list.Remove(node);
        }
        else if (_index.ContainsKey(key) && _index[key].Equals(node))
        {
            var nextNode = _index[key].Next;
            if (nextNode == null)
            {
                _index.Remove(key);
            }
            else
            {
                var nextNodeKey = (int)nextNode.Value.Key >> _accuracy;
                if (key != nextNodeKey)
                {
                    _index.Remove(key);
                }
                else
                {
                    _index[key] = nextNode;
                }
            }

            _list.Remove(node);
        }
    }


    bool ICollection<T>.Remove(T item)
    {
        return Remove(item);
    }

    public bool Remove(T entryToRemove)
    {
        var node = _list.First;
        while (node != null)
        {
            if (node.Value.Value.Equals(entryToRemove))
            {
                Remove(node);
                return true;
            }

            node = node.Next;
        }

        return false;
    }

    public void Clear()
    {
        _index.Clear();
        _list.Clear();
    }

    public bool Contains(T item)
    {
        return _list.Any(node => node.Value.Equals(item));
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    private LinkedListNode<SortedLinkedListNode<T>> AddItemToBin(T value, double sortingKey,
        LinkedListNode<SortedLinkedListNode<T>>? node, int key)
    {
        while (node != null)
        {
            if (node.Value.Key > sortingKey)
            {
                break;
            }

            node = node.Next;
        }

        LinkedListNode<SortedLinkedListNode<T>> newNode;
        if (node == null)
        {
            newNode = _list.AddLast(new SortedLinkedListNode<T>(sortingKey, value));
        }
        else
        {
            newNode = _list.AddBefore(node, new SortedLinkedListNode<T>(sortingKey, value));
        }

        if (!_index.ContainsKey(key) || _index[key].Equals(node))
        {
            _index[key] = newNode;
        }

        return newNode;
    }

    private int GetKey(double sortingKey)
    {
        return (int)(sortingKey * 1000) >> _accuracy;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new SortedLinkedListEnumerator<T>(_list);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public class SortedLinkedListEnumerator<T> : IEnumerator<T>
{
    private readonly LinkedList<SortedLinkedListNode<T>> _list;
    private LinkedListNode<SortedLinkedListNode<T>>? _node;

    public SortedLinkedListEnumerator(LinkedList<SortedLinkedListNode<T>> list)
    {
        _list = list;
        _node = list.First;
    }

    public bool MoveNext()
    {
        _node = _node?.Next;
        return _node?.Next != null;
    }

    public void Reset()
    {
        _node = _list.First;
    }

    public T Current => _node.Value.Value;

    object? IEnumerator.Current => _node?.Value;

    public void Dispose()
    {
    }
}