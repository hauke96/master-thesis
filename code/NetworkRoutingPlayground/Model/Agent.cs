using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetworkRoutingPlayground.Layer;
using ServiceStack;
using ServiceStack.Text;
using Wavefront.IO;

namespace NetworkRoutingPlayground.Model
{
    public class Agent : IAgent<VectorLayer>, ISpatialGraphEntity
    {
        private static readonly double STEP_SIZE = 2;
        private static readonly Random RANDOM = new((int)DateTime.Now.ToUnixTime());

        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }
        [PropertyDescription] public NetworkLayer NetworkLayer { get; set; }

        public Position? Position { get; set; }
        public Guid ID { get; set; } = Guid.NewGuid();

        private Route _route;

        // Spatial entity stuff:
        public double Length => 0.0;
        public ISpatialEdge CurrentEdge { get; set; }
        public double PositionOnCurrentEdge { get; set; }
        public int LaneOnCurrentEdge { get; set; }
        public SpatialModalityType ModalityType => SpatialModalityType.Walking;
        public bool IsCollidingEntity => false;

        public void Init(VectorLayer layer)
        {
            // var startNode = NetworkLayer.Environment.NearestNode(new Position(0.5, 0));
            // var destinationNode = NetworkLayer.Environment.NearestNode(new Position(1.5, 2));

            var allNodes = FindNodesByKey("poi");
            // var allNodes = NetworkLayer.Environment.Nodes.ToList();
            var startNode = allNodes[RANDOM.Next(allNodes.Count)];
            var destinationNode = startNode;
            while (destinationNode == startNode)
            {
                destinationNode = allNodes[RANDOM.Next(allNodes.Count)];
            }

            // var allNodes = NetworkLayer.Environment.Nodes.ToList();
            // var startNode = allNodes.First(node => node.Index == 9);
            // var destinationNode = allNodes.First(node => node.Index == 37);

            _route = NetworkLayer.Environment
                .FindRoute(startNode, destinationNode,
                    (from, via, to) => via.Length * (via.Attributes.IsEmpty() ? 1 : 0.1));
            NetworkLayer.Environment.Insert(this, startNode);

            Exporter.WriteRouteToFile(_route.SelectMany(edgeStop => edgeStop.Edge.Geometry).ToList());
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
            if (_route.GoalReached)
            {
                Kill();
                return;
            }

            var moved = NetworkLayer.Environment.Move(this, _route, STEP_SIZE);
            if (!moved)
            {
                Kill();
                return;
            }

            Position = this.CalculateNewPositionFor(_route, out _);
        }

        private void Kill()
        {
            Console.WriteLine("Agent reached target");
            NetworkLayer.Environment.Remove(this);
            UnregisterHandle.Invoke(NetworkLayer, this);
        }

        private List<ISpatialNode> FindNodesByKey(string attributeName)
        {
            return NetworkLayer.Environment.Nodes.Where(node => node.Attributes.ContainsKey(attributeName))
                .ToList();
        }
    }
}