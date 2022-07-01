using Mars.Interfaces.Environments;

namespace RoutingWithLineObstacle.Wavefront.Events
{
    public class VertexEvent : IComparable<VertexEvent>
    {
        public Position Position { get; }
        public Position Root { get; }
        public double Distance { get; }

        public VertexEvent(Position position, Position root)
        {
            Position = position;
            Root = root;
            Distance = root.DistanceInMTo(position);
        }

        public int CompareTo(VertexEvent? other)
        {
            return (int)(Distance - other.Distance);
        }
    }
}