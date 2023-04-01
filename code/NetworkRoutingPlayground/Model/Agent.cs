using Mars.Common.Collections.Graph;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetworkRoutingPlayground.Layer;
using ServiceStack;
using ServiceStack.Text;
using Wavefront.Geometry;
using Wavefront.IO;

namespace NetworkRoutingPlayground.Model
{
    public class Agent : IAgent<AgentLayer>, IPositionable
    {
        private static readonly double STEP_SIZE = 2;
        private static readonly Random RANDOM = new((int)DateTime.Now.ToUnixTime());

        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }
        [PropertyDescription] public NetworkLayer NetworkLayer { get; set; }

        public Position? Position { get; set; }
        public Guid ID { get; set; } = Guid.NewGuid();

        private AgentLayer _agentLayer;

        private IList<EdgeData> _shortestPath;
        private List<Position> _waypoints;

        // Spatial entity stuff:
        public double Length => 0.0;
        public ISpatialEdge CurrentEdge { get; set; }
        public double PositionOnCurrentEdge { get; set; }
        public int LaneOnCurrentEdge { get; set; }
        public SpatialModalityType ModalityType => SpatialModalityType.Walking;
        public bool IsCollidingEntity => false;

        public void Init(AgentLayer layer)
        {
            _agentLayer = layer;
            _agentLayer.InitEnvironment(NetworkLayer.Graph);
            
            // var startNode = NetworkLayer.Environment.NearestNode(new Position(0.5, 0));
            // var destinationNode = NetworkLayer.Environment.NearestNode(new Position(1.5, 2));

            var allNodes = FindNodesByKey("poi");
            // var allNodes = NetworkLayer.Environment.Nodes.ToList();
            var startNode = allNodes[RANDOM.Next(allNodes.Count)];
            var destinationNode = startNode;
            while (destinationNode.Key == startNode.Key)
            {
                destinationNode = allNodes[RANDOM.Next(allNodes.Count)];
            }

            // var allNodes = NetworkLayer.Environment.Nodes.ToList();
            // var startNode = allNodes.First(node => node.Index == 9);
            // var destinationNode = allNodes.First(node => node.Index == 37);

            _waypoints = NetworkLayer.Graph.ShortestPath(new Position(9.9932553, 53.5536623), destinationNode.Position)
                .Map(e => e.Geometry).SelectMany(x => x).ToList();

            Position = _waypoints[0];
            _waypoints.RemoveAt(0);

            // Exporter.WriteRouteToFile(_route.SelectMany(edgeStop => edgeStop.Edge.Geometry).ToList());
        }

        public void Tick()
        {
            try
            {
                TickInternal();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void TickInternal()
        {
            if (_waypoints.IsEmpty())
            {
                Kill();
                return;
            }

            _agentLayer.Environment.MoveTo(this, _waypoints[0], STEP_SIZE);

            if (Position.DistanceInMTo(_waypoints[0]) < STEP_SIZE)
            {
                if (_waypoints.Count > 1)
                {
                    _waypoints.RemoveAt(0);
                }
                else
                {
                    // Agent reached last waypoint
                    _waypoints.Clear();
                    Kill();
                }
            }
        }

        private void Kill()
        {
            Console.WriteLine("Agent reached target");
            _agentLayer.Environment.Remove(this);
            UnregisterHandle.Invoke(NetworkLayer, this);
        }

        private List<NodeData> FindNodesByKey(string attributeName)
        {
            return NetworkLayer.Graph.GetNodesByAttribute(attributeName);
        }
    }
}