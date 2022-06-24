using Mars.Components.Environments.Cartesian;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;

namespace Model
{
    public class Character : ICharacter, IAgent<VectorLayer>
    {
        public Position Position { get; set; }
        public double Extent { get; set; }
        public Guid ID { get; set; }

        public void Init(VectorLayer layer)
        {
            Position = Position.CreatePosition(0.05, 0.05);
            // SharedEnvironment.Environment.Insert(this, Position);
            SharedEnvironment.Environment.Insert(this);
        }

        public void Tick()
        {
            // SharedEnvironment.Environment.Move(this, 45, 10);
            SharedEnvironment.Environment.MoveTowards(this, 45, 10);
        }

        public CollisionKind? HandleCollision(ICharacter other)
        {
            throw new NotImplementedException();
        }
    }
}