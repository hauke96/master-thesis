using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Mars.Common.Core;
using Mars.Common.Data;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront;
using Wavefront.Geometry;

namespace HikerModel.Model
{
    public class ObstacleLayer : VectorLayer
    {
        public WavefrontAlgorithm WavefrontAlgorithm { get; private set; }

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var initLayer = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
            if (!initLayer)
            {
                return false;
            }
            
            var obstacleGeometries = Features.Map(f => new Obstacle(f.VectorStructured.Geometry));
            WavefrontAlgorithm = new WavefrontAlgorithm(obstacleGeometries);

            return true;
        }
    }
}