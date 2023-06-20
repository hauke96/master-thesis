using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;
using ServiceStack;

namespace HikerModel.Model
{
    public class HikerLayer : AbstractLayer
    {
        public GeoHashEnvironment<Hiker> Environment { get; private set; }
        public Envelope BBOX;

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            agentManager.Spawn<Hiker, HikerLayer>().ToList();

            return true;
        }

        public void InitEnvironment(ICollection<IVectorFeature> features, Hiker hiker)
        {
            BBOX = features.CreateCopy().Map(f => f.VectorStructured.BoundingBox)
                .Aggregate((e1, e2) => e1.ExpandedBy(e2));
            
            Environment = GeoHashEnvironment<Hiker>.BuildEnvironment(BBOX.MaxY, BBOX.MinY, BBOX.MinX, BBOX.MaxX, 10);
            Environment.Insert(hiker);
        }
    }
}