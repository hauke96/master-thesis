using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Tests.Graph;

public class HybridVisibilityGraphGeneratorTest
{
    [Test]
    public void GetObstacles()
    {
        var featureObstacle = new Feature(
            new LineString(new[] { new Coordinate(0, 0), new Coordinate(1, 1) }),
            new AttributesTable(
                new Dictionary<string, object> { { "building", "yes" } }
            )
        );
        var featureNonObstacle = new Feature(
            new LineString(new[] { new Coordinate(0, 2), new Coordinate(1, 3) }),
            new AttributesTable(
                new Dictionary<string, object> { { "foo", "bar" } }
            )
        );

        var obstacles = HybridVisibilityGraphGenerator.GetObstacles(new[] { featureObstacle, featureNonObstacle })
            .QueryAll();

        Assert.AreEqual(1, obstacles.Count);
        var obstacle = obstacles[0];
        CollectionAssert.AreEqual(featureObstacle.Geometry.Coordinates, obstacle.Coordinates);
        Assert.AreEqual(2, obstacle.Vertices.Count);
        Assert.AreEqual(obstacle.Coordinates[0].ToPosition(), obstacle.Vertices[0].Position);
        Assert.AreEqual(obstacle.Coordinates[1].ToPosition(), obstacle.Vertices[1].Position);
    }

    [Test]
    public void GetObstacles_usesTriangulation()
    {
        var featureObstacle = new Feature(
            new Polygon(
                new LinearRing(new[]
                {
                    new Coordinate(0, 0),
                    new Coordinate(1, 0),
                    new Coordinate(1, 1),
                    new Coordinate(0, 1),
                    new Coordinate(0, 0)
                })
            ),
            new AttributesTable(
                new Dictionary<string, object> { { "building", "yes" } }
            )
        );

        var obstacles = HybridVisibilityGraphGenerator.GetObstacles(new[] { featureObstacle }).QueryAll();

        Assert.AreEqual(2, obstacles.Count);

        var obstacle = obstacles[0];
        Assert.AreEqual(4, obstacle.Coordinates.Count);
        CollectionAssert.IsSupersetOf(featureObstacle.Geometry.Coordinates.Distinct(),
            obstacle.Coordinates.Distinct());

        obstacle = obstacles[1];
        Assert.AreEqual(4, obstacle.Coordinates.Count);
        CollectionAssert.IsSupersetOf(featureObstacle.Geometry.Coordinates.Distinct(),
            obstacle.Coordinates.Distinct());
    }

    [Test]
    public void DetermineVisibilityNeighbors()
    {
        var obstacle1 = new Obstacle(new LineString(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(2, 0)
        }));
        var obstacle2 = new Obstacle(new LineString(new[]
        {
            new Coordinate(0.5, 1),
            new Coordinate(10, 1)
        }));
        var obstacle3 = new Obstacle(new LineString(new[]
        {
            new Coordinate(0, 2),
            new Coordinate(1, 2),
            new Coordinate(2, 2)
        }));

        var obstacleIndex = new QuadTree<Obstacle>();
        obstacleIndex.Insert(obstacle1.Envelope, obstacle1);
        obstacleIndex.Insert(obstacle2.Envelope, obstacle2);
        obstacleIndex.Insert(obstacle3.Envelope, obstacle3);

        var visibilityNeighbors = HybridVisibilityGraphGenerator.DetermineVisibilityNeighbors(obstacleIndex);

        CollectionAssert.AreEquivalent(VisibilityGraphGenerator.CalculateVisibleKnn(obstacleIndex, 36, 10, true),
            visibilityNeighbors);
    }

    [Test]
    public void AddVisibilityVerticesAndEdges()
    {
        Log.LogLevel = Log.DEBUG;

        // Dummy obstacles because the hybrid graph also contains all obstacles.
        var obstacle = new Obstacle(new Polygon(new LinearRing(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(1, 1),
            new Coordinate(0, 0)
        })));
        var obstacles = new QuadTree<Obstacle>();
        obstacles.Insert(obstacle.Envelope, obstacle);

        // Vertices:
        // Vertex at bottom
        var bottomVertex = new Vertex(new Position(1, 0));
        // Middle line with tree vertices
        var middleVertex0 = new Vertex(new Position(0, 1), new Position(1, 1));
        var middleVertex1 = new Vertex(new Position(1, 1), new Position(0, 1), new Position(2, 1));
        var middleVertex2 = new Vertex(new Position(2, 1), new Position(1, 1));
        // Top line with two vertices
        var topVertex0 = new Vertex(new Position(0, 2), new Position(1, 2));
        var topVertex1 = new Vertex(new Position(1, 2), new Position(0, 2));

        // Create visibility relations without the use of additional function to make sure the setup is always correct.
        var vertexNeighbors = new Dictionary<Vertex, List<List<Vertex>>>();

        vertexNeighbors.Add(bottomVertex,
            new List<List<Vertex>> { new() { middleVertex0, middleVertex1, middleVertex2 } });

        vertexNeighbors.Add(middleVertex0,
            new List<List<Vertex>> { new() { bottomVertex, topVertex0, topVertex1, middleVertex1 }, });
        vertexNeighbors.Add(middleVertex1,
            new List<List<Vertex>>
            {
                new() { middleVertex0, topVertex0, topVertex1, middleVertex2 },
                new() { middleVertex0, bottomVertex, middleVertex2 }
            });
        vertexNeighbors.Add(middleVertex2,
            new List<List<Vertex>> { new() { middleVertex1, topVertex0, topVertex1, bottomVertex }, });

        vertexNeighbors.Add(topVertex0,
            new List<List<Vertex>> { new() { middleVertex0, middleVertex1, middleVertex2, topVertex1 } });
        vertexNeighbors.Add(topVertex1,
            new List<List<Vertex>> { new() { middleVertex0, middleVertex1, middleVertex2, topVertex0 } });

        // Act
        var (hybridVisibilityGraph, spatialGraph) =
            HybridVisibilityGraphGenerator.AddVisibilityVerticesAndEdges(vertexNeighbors, obstacles);

        // Assert
        Assert.AreEqual(7, spatialGraph.NodesMap.Count);

        var vertexToNode = new Dictionary<Vertex, List<int>>();
        vertexNeighbors.Keys.Each(vertex =>
        {
            vertexToNode.Add(vertex, new List<int>());
            foreach (var (key, nodeData) in spatialGraph.NodesMap)
            {
                if (nodeData.Position.DistanceInMTo(vertex.Position) < 0.001)
                {
                    vertexToNode[vertex].Add(key);
                }
            }
        });

        // Check is all wanted edges exist. For The vertex "middleVertex1" there should be two nodes, so we have to
        // consider the edges starting/ending at this vertex separately. First consider all simple edges with unique
        // vertex-nodes-mapping.
        // Note: All edges are undirected, which means for every connection between two nodes, there are two edges.
        List<int> nodes;
        int node;
        List<EdgeData> edges;

        // The middle vertex 1 is split into two nodes, one for the vertices above the middle line and one for the
        // vertices below that line. We here find out what node relates to what side of the middle line.
        int middleVertex1NodeTop, middleVertex1NodeBottom;
        if (spatialGraph.EdgesMap.ContainsKey((vertexToNode[bottomVertex][0], vertexToNode[middleVertex1][0])))
        {
            // There's an edge from the bottom vertex to node [0] of the middle vertex 1.
            middleVertex1NodeTop = vertexToNode[middleVertex1][1];
            middleVertex1NodeBottom = vertexToNode[middleVertex1][0];
        }
        else
        {
            // There's NO edge from the bottom vertex to node [0] of the middle vertex 1. This means the node [1] of
            // the middle vertex belongs to the bottom side of the middle line.
            middleVertex1NodeTop = vertexToNode[middleVertex1][0];
            middleVertex1NodeBottom = vertexToNode[middleVertex1][1];
        }

        // bottomVertex
        nodes = vertexToNode[bottomVertex];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(6, edges.Count);

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex0][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, middleVertex1NodeBottom)));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((middleVertex1NodeBottom, node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex2][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex2][0], node)));

        // middleVertex0
        nodes = vertexToNode[middleVertex0];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(10, edges.Count);

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[bottomVertex][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[bottomVertex][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, middleVertex1NodeTop)));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((middleVertex1NodeTop, node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, middleVertex1NodeBottom)));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((middleVertex1NodeBottom, node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[topVertex1][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[topVertex1][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[topVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[topVertex0][0], node)));

        // middleVertex1
        nodes = vertexToNode[middleVertex1];
        Assert.AreEqual(2, nodes.Count);

        // middleVertex1 - top node
        node = middleVertex1NodeTop;
        edges = spatialGraph.Edges.Values
            .Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(8, edges.Count);

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex0][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[topVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[topVertex0][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[topVertex1][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[topVertex1][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex2][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex2][0], node)));

        // middleVertex1 - bottom node
        node = middleVertex1NodeBottom;
        edges = spatialGraph.Edges.Values
            .Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(6, edges.Count);

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex0][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[bottomVertex][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[bottomVertex][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex2][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex2][0], node)));

        // middleVertex2
        nodes = vertexToNode[middleVertex2];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(10, edges.Count);

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[bottomVertex][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[bottomVertex][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, middleVertex1NodeTop)));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((middleVertex1NodeTop, node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, middleVertex1NodeBottom)));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((middleVertex1NodeBottom, node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[topVertex1][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[topVertex1][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[topVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[topVertex0][0], node)));

        // topVertex0
        nodes = vertexToNode[topVertex0];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(8, edges.Count);

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex0][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, middleVertex1NodeTop)));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((middleVertex1NodeTop, node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex2][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex2][0], node)));

        // topVertex1
        nodes = vertexToNode[topVertex1];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(8, edges.Count);

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex0][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex0][0], node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, middleVertex1NodeTop)));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((middleVertex1NodeTop, node)));

        Assert.True(spatialGraph.EdgesMap.ContainsKey((node, vertexToNode[middleVertex2][0])));
        Assert.True(spatialGraph.EdgesMap.ContainsKey((vertexToNode[middleVertex2][0], node)));
    }
}