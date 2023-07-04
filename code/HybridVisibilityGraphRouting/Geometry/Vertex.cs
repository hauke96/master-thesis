using Mars.Common;
using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Geometry;

public class Vertex
{
    private static int ID_COUNTER;

    private int Id { get; }

    /// <summary>
    /// The neighbors are neighboring vertices on obstacles but not across open spaces. These are not the visible
    /// vertices one might obtain by running the knn-search to find all k many visible neighbors. This list is sorted by
    /// the angle of the vertex position to each neighbor.
    /// In other words: There is an edge from this vertex to these neighboring vertices in the (preprocessed) dataset. 
    /// </summary>
    public List<Position> ObstacleNeighbors { get; private set; }

    public List<(double, double)> ValidAngleAreas { get; private set; }

    public Coordinate Coordinate { get; }
    public bool IsOnConvexHull { get; }

    public Vertex(Coordinate coordinate) : this(coordinate, new List<Position>(), false)
    {
    }

    public Vertex(Coordinate coordinate, bool isOnConvexHull) : this(coordinate, new List<Position>(), isOnConvexHull)
    {
    }

    /// <param name="obstacleNeighbors">
    /// The neighbors are neighboring vertices on obstacles but not across open spaces. These are not the visible
    /// vertices one might obtain by running the knn-search to find all n many visible neighbors.
    /// </param>
    // TODO Only used in test code -> Merge with constructor above?
    public Vertex(Coordinate coordinate, IEnumerable<Position> obstacleNeighbors, bool isOnConvexHull)
    {
        Coordinate = coordinate;
        ObstacleNeighbors = obstacleNeighbors.ToList();
        Id = ID_COUNTER++;
        IsOnConvexHull = isOnConvexHull;
        SortObstacleNeighborsByAngle();
    }

    /// <summary>
    /// Needs to be called after the obstacle neighbors changed. This is intentionally *not* done automatically due to
    /// performance reasons.
    /// This method also updates the valid angle areas.
    /// </summary>
    public void SortObstacleNeighborsByAngle()
    {
        ObstacleNeighbors = ObstacleNeighbors.OrderBy(n => Angle.GetBearing(Coordinate, n.ToCoordinate())).ToList();
        ValidAngleAreas = CalculateValidAngleAreas();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Vertex);
    }


    /// <summary>
    /// Calculates the valid angle areas for the given vertex.
    ///
    /// A valid angle area is an angle area in which potential visibility neighbors are. Not all neighboring nodes
    /// are potential visibility neighbors since not all resulting visibility edges would be part of shortest paths.
    /// To avoid unnecessary checks, only nodes which have a chance to be on a shortest path are selected. To do this,
    /// certain angle areas are used to exclude irrelevant nodes.
    ///
    /// Valid angle areas are determined depending on the obstacle neighbors of the given vertex. Angle areas that
    /// would enlarge the convex hull of the obstacle (without removing the given vertex from it) are determined.
    /// </summary>
    private List<(double, double)> CalculateValidAngleAreas()
    {
        if (ObstacleNeighbors.Count <= 1)
        {
            return new List<(double, double)>
            {
                (0, 360)
            };
        }

        var validAngleAreas = new List<(double, double)>();
        // We only need to search for two obstacle neighbors with an angle area of >180°. All other angle areas
        // form concave parts and are therefore irrelevant. Only up to one angle between two adjacent obstacle
        // neighbors can be >180°. This means none or two valid angle areas exist.
        ObstacleNeighbors
            .Each((thisIndex, neighbor) =>
            {
                var nextIndex = thisIndex + 1;
                if (thisIndex == ObstacleNeighbors.Count - 1)
                {
                    nextIndex = 0;
                }

                var angleFrom = Angle.GetBearing(Coordinate.ToPosition(), neighbor);
                var angleTo = Angle.GetBearing(Coordinate.ToPosition(), ObstacleNeighbors[nextIndex]);

                if (Angle.Difference(angleFrom, angleTo) >= 180)
                {
                    validAngleAreas.Add((angleFrom, Angle.Normalize(angleTo - 180)));
                    validAngleAreas.Add((Angle.Normalize(angleFrom - 180), angleTo));
                }
            });

        return validAngleAreas;
    }

    private bool Equals(Vertex? other)
    {
        return other != null && Coordinate.Equals(other.Coordinate);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Coordinate);
    }

    public override string ToString()
    {
        return "v#" + Id + " : " + Coordinate;
    }
}