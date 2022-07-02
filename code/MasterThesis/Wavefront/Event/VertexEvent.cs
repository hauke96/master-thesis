using Mars.Interfaces.Environments;
using Wavefront.Geometry;

namespace RoutingWithLineObstacle.Wavefront.Events
{
    public class VertexEvent : IComparable<VertexEvent>
    {
        public Vertex Vertex { get; }
        public Position Position => Vertex.Position;
        
        public Position Root { get; }

        /// <summary>
        /// The distance from the source of the routing to the root of this event.
        /// </summary>
        public double DistanceToRootFromSource { get; }

        /// <summary>
        /// The distance from the source of the routing via the root of this event to the actual event location.
        /// </summary>
        public double DistanceFromSource { get; }

        public VertexEvent(Vertex vertex, Position root, double distanceToRootFromSource)
        {
            Vertex = vertex;
            Root = root;
            DistanceToRootFromSource = distanceToRootFromSource;
            DistanceFromSource = DistanceToRootFromSource + root.DistanceInMTo(Position);
        }

        public int CompareTo(VertexEvent? other)
        {
            return (int)(DistanceFromSource - other.DistanceFromSource);
        }
    }
}