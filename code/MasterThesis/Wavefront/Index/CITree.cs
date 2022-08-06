using Mars.Common.Core.Collections;
using NetTopologySuite.Index.Bintree;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront.Index;

public class CITreeNode<T>
{
    public double From { get; }
    public double To { get; }
    public T Value { get; }

    public CITreeNode(double from, double to, T value)
    {
        From = from;
        To = to;
        Value = value;
    }
}

/// <summary>
/// This is a one dimensional R-tree but the one dimension is a circle so every range values are modulo 360.
/// </summary>
public class CITree<T> : Bintree<CITreeNode<T>>
{
    public void Insert(Double from, Double to, T value)
    {
        from = Angle.Normalize(from);
        to = Angle.StrictNormalize(to);

        var exceedsZeroDegree = Angle.IsBetween(from, 0, to) || to == 0;

        if (exceedsZeroDegree)
        {
            base.Insert(new Interval(from, 360), new CITreeNode<T>(from, 360, value));
            base.Insert(new Interval(0, to), new CITreeNode<T>(0, to, value));
        }
        else
        {
            base.Insert(new Interval(from, to), new CITreeNode<T>(from, to, value));
        }
    }

    public IList<T> Query(double at)
    {
        // var result = new LinkedList<T>();
        // base.Query(new Interval(at, at), result);
        // return result;
        return Query(at, at);
    }

    public IList<T> Query(double from, double to)
    {
        from = Angle.Normalize(from);
        to = Angle.Normalize(to);

        var exceedsZeroDegree = Angle.IsBetweenEqual(from, 360, to);

        if (exceedsZeroDegree)
        {
            var result = new LinkedList<CITreeNode<T>>();
            result.AddRange(QueryExact(from, 360));
            result.AddRange(QueryExact(0, to));
            return result.Map(node => node.Value).Distinct().ToList();
        }

        return QueryExact(from, to).Map(node => node.Value);
    }

    private IEnumerable<CITreeNode<T>> QueryExact(double from, double to)
    {
        return base.Query(new Interval(from, to)).Where(node =>
        {
            return Angle.GreaterEqual(node.To, from) && Angle.LowerEqual(node.From, to);
        });
    }

    // public void Remove(double from, double to, T value)
    // {
    //     base.Remove(new Interval(from, to), value);
    // }
}