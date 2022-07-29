namespace Wavefront;

public class SortedLinkedListNode<T>
{
    public T Value { get; }
    public double Key { get; set; }
    public double BearingFromWavefront { get; }

    public SortedLinkedListNode(double key, T value, double bearingFromWavefront)
    {
        Key = key;
        Value = value;
        BearingFromWavefront = bearingFromWavefront;
    }

    public override string ToString()
    {
        return $"(key={Key}, value={Value}";
    }
}