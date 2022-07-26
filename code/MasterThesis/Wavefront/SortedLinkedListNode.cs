namespace Wavefront;

public class SortedLinkedListNode<T>
{
    public T Value { get; }
    public double Key { get; }

    public SortedLinkedListNode(double key, T value)
    {
        Key = key;
        Value = value;
    }
}