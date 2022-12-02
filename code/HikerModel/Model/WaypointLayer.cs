using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Mars.Common.Core;
using Mars.Common.Data;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;
using Wavefront;
using Wavefront.Geometry;

namespace HikerModel.Model
{
    public class WaypointLayer : VectorLayer
    {
        public IEnumerable<Coordinate> TrackPoints { get; set; }

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var initLayer = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            TrackPoints = layerInitData.LayerInitConfig.Inputs.Import()
                .OfType<IStructuredDataGeometry>()
                .OrderBy(geometry => geometry.Data["track_seg_point_id"].Value<int>())
                .SelectMany(geometry => geometry.Geometry.Coordinates);

            return initLayer;
        }
    }
}