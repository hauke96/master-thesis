using Mars.Interfaces.Environments;

namespace RoutingWithLineObstacle.Wavefront.Events
{
    public class VertexEvent : IComparable<VertexEvent>
    {
        public Position Position { get; }
        public Position Root { get; }

        /// <summary>
        /// The distance from the source of the routing to the root of this event.
        /// </summary>
        public double DistanceToRootFromSource { get; }

        /// <summary>
        /// The distance from the source of the routing via the root of this event to the actual event location.
        /// </summary>
        public double DistanceFromSource { get; }

        public VertexEvent(Position position, Position root, double distanceToRootFromSource)
        {
            Position = position;
            Root = root;
            DistanceToRootFromSource = distanceToRootFromSource;
            DistanceFromSource = DistanceToRootFromSource + root.DistanceInMTo(position);
        }

        public int CompareTo(VertexEvent? other)
        {
            return (int)(DistanceFromSource - other.DistanceFromSource);
        }
    }
}