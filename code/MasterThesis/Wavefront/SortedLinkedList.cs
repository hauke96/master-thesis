using System.Collections;

namespace Wavefront;

public class SortedLinkedList<T> : ICollection<T>
{
    private LinkedListNode<SortedLinkedListNode<T>>?[] _index;
    private readonly int _accuracy;
    private readonly LinkedList<SortedLinkedListNode<T>> _list;

    public int Count => _list.Count;
    public bool IsReadOnly => false;
    public LinkedListNode<SortedLinkedListNode<T>>? First => Count > 0 ? _list.First : null;

    public SortedLinkedList(int accuracy = 0)
    {
        _list = new LinkedList<SortedLinkedListNode<T>>();
        _index = new LinkedListNode<SortedLinkedListNode<T>>[100];
        _accuracy = accuracy;
    }

    public void Add(T item)
    {
        throw new NotImplementedException();
    }

    public void AddLast(SortedLinkedListNode<T> node)
    {
        if (_list.Last != null && _list.Last.Value.Key > node.Key)
        {
            throw new InvalidOperationException();
        }

        var indexKey = GetKey(node.Key);
        var newNode = _list.AddLast(node);
        if (_index[indexKey] == null)
        {
            _index[indexKey] = newNode;
        }
    }

    public void Add(T value, double sortingKey, double bearingFromWavefront)
    {
        var indexKey = GetKey(sortingKey);
        var node = _index[indexKey];
        if (node == null)
        {
            node = _list.First;
        }

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
            newNode = _list.AddLast(new SortedLinkedListNode<T>(sortingKey, value, bearingFromWavefront));
        }
        else
        {
            newNode = _list.AddBefore(node, new SortedLinkedListNode<T>(sortingKey, value, bearingFromWavefront));
        }

        if (_index[indexKey] == null || sortingKey < _index[indexKey].Value.Key)
        {
            _index[indexKey] = newNode;
        }
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
        var indexContainsKey = IndexContains(key);

        if (!indexContainsKey || indexContainsKey && !_index[key]!.Equals(node))
        {
            _list.Remove(node);
        }
        else if (indexContainsKey && _index[key]!.Equals(node))
        {
            var nextNode = _index[key]!.Next;
            if (nextNode == null)
            {
                _index[key] = null;
            }
            else
            {
                var nextNodeKey = GetKey(nextNode.Value.Key);
                if (key != nextNodeKey)
                {
                    _index[key] = null;
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
        _index = new LinkedListNode<SortedLinkedListNode<T>>[2000];
        _list.Clear();
    }

    public bool Contains(T item)
    {
        return _list.Any(node => node.Value.Equals(item));
    }

    private bool IndexContains(int key)
    {
        return _index[key] != null;
    }

    private int GetKey(double sortingKey)
    {
        var key = (int)(sortingKey * 100000) >> _accuracy;
        if (key >= _index.Length)
        {
            return _index.Length - 1;
        }

        return key;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        var node = _list.First;
        for (var i = 0; i < _list.Count; i++)
        {
            if (i == arrayIndex)
            {
                array[arrayIndex] = node.Value.Value;
            }
        }

        throw new InvalidOperationException();
    }

    public struct Enumerator : IEnumerator<T>
    {
        private readonly SortedLinkedList<T> _list;
        private LinkedListNode<SortedLinkedListNode<T>> _node;
        private T _current;

        internal Enumerator(SortedLinkedList<T> list)
        {
            _list = list;
            _node = list.First;
            _current = default;
        }

        public T Current => _current;

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_node == null)
            {
                return false;
            }

            _current = _node.Value.Value;
            _node = _node.Next;

            return true;
        }

        void IEnumerator.Reset()
        {
            _current = default;
            _node = _list.First;
        }

        public void Dispose()
        {
        }
    }
}