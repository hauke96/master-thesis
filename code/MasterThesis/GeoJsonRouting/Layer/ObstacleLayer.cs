using GeoJsonRouting.Model;
using Mars.Common;
using Mars.Components.Layers;
using Position = Mars.Interfaces.Environments.Position;

namespace GeoJsonRouting.Layer
{
    public class ObstacleLayer : VectorLayer
    {
        public Position GetStart()
        {
            return FindOrCreateLocation("start");
        }

        public Position GetTarget()
        {
            return FindOrCreateLocation("target");
        }

        private Position FindOrCreateLocation(string attributeName)
        {
            var startPosition = Features.First(feature => feature.VectorStructured.Attributes.Exists(attributeName))
                .VectorStructured.Geometry.Coordinates[0].ToPosition();

            return startPosition ??
                   PositionHelper.RandomPositionFromGeometry(SharedEnvironment.Environment.BoundingBox);
        }
    }
}