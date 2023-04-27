using System.Collections.Generic;
using System.Linq;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
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
    class WithGeneratedGraph : HybridVisibilityGraphGeneratorTest
    {
        Feature featureObstacle1;
        Feature featureObstacle2;
        Feature featureRoad1;
        Feature featureRoad2;

        HybridVisibilityGraph hybridVisibilityGraph;
        SpatialGraph graph;

        [SetUp]
        public void setup()
        {
            /*
             *        (obst.)
             *      x----------x
             * 
             *  x - - - - x - - - x (road)
             *            '
             *   x---x    '
             *       |    ' (road)
             *       x    '
             * (obst.)    x
             */
            featureObstacle1 = new Feature(
                new LineString(new[] { new Coordinate(0.5, 2), new Coordinate(2, 2) }),
                new AttributesTable(
                    new Dictionary<string, object> { { "building", "yes" } }
                )
            );
            featureObstacle2 = new Feature(
                new LineString(new[]
                {
                    new Coordinate(1, 0),
                    new Coordinate(1, 1),
                    new Coordinate(0, 1),
                }),
                new AttributesTable(
                    new Dictionary<string, object> { { "building", "yes" } }
                )
            );
            featureRoad1 = new Feature(
                new LineString(new[] { new Coordinate(-1, 1.5), new Coordinate(1.5, 1.5), new Coordinate(3, 1.5) }),
                new AttributesTable(
                    new Dictionary<string, object> { { "highway", "road" } }
                )
            );
            featureRoad2 = new Feature(
                new LineString(new[] { new Coordinate(1.5, 1.5), new Coordinate(1.5, -1) }),
                new AttributesTable(
                    new Dictionary<string, object> { { "highway", "road" } }
                )
            );

            var features = new List<IFeature>
            {
                featureObstacle1,
                featureObstacle2,
                featureRoad1,
                featureRoad2
            };

            // Act
            hybridVisibilityGraph = HybridVisibilityGraphGenerator.Generate(features);
            graph = hybridVisibilityGraph.Graph;
        }

        [Test]
        public void Generate()
        {
            // Asserts
            var obstacle1Coord0 = GetNode(graph, featureObstacle1.Geometry.Coordinates[0]);
            var obstacle1Coord1 = GetNode(graph, featureObstacle1.Geometry.Coordinates[1]);

            var obstacle2Coord0 = GetNode(graph, featureObstacle2.Geometry.Coordinates[0]);
            var obstacle2Coord1 = GetNode(graph, featureObstacle2.Geometry.Coordinates[1]);
            var obstacle2Coord2 = GetNode(graph, featureObstacle2.Geometry.Coordinates[2]);

            // Find intersection nodes on the road segments
            var intersection_O1C0_O2C1 = GetNode(graph, new Coordinate(0.75, 1.5));
            var intersection_O1C0_O2C2 = GetNode(graph, new Coordinate(0.25, 1.5));

            var intersection_O1C1_O2C0_road1 = GetNode(graph, new Coordinate(1.75, 1.5));
            var intersection_O1C1_O2C0_road2 = GetNode(graph, new Coordinate(1.5, 1));
            var intersection_O1C1_O2C1 = GetNode(graph, new Coordinate(1.5, 1.5));
            var intersection_O1C1_O2C2 = GetNode(graph, new Coordinate(1, 1.5));

            // All edges on obstacle1 should exist
            AssertEdges(graph, obstacle1Coord0, obstacle1Coord1);

            // All edges on obstacle2 should exist
            AssertEdges(graph, obstacle2Coord0, obstacle2Coord1);
            AssertEdges(graph, obstacle2Coord1, obstacle2Coord2);

            // Intersections between Obstacle1-Coordinate0 and Obstacle2
            AssertEdges(graph, obstacle1Coord0, intersection_O1C0_O2C2);
            AssertEdges(graph, intersection_O1C0_O2C2, obstacle2Coord2);

            AssertEdges(graph, obstacle1Coord0, intersection_O1C0_O2C1);
            // Cannot be tested properly due to multiple nodes at the location: AssertEdges(graph, intersection_O1C0_O2C1, obstacle2Coord1);

            // Intersections between Obstacle1-Coordinate1 and Obstacle2
            AssertEdges(graph, obstacle1Coord1, intersection_O1C1_O2C0_road1);
            AssertEdges(graph, intersection_O1C1_O2C0_road1, intersection_O1C1_O2C0_road2);
            AssertEdges(graph, intersection_O1C1_O2C0_road2, obstacle2Coord0);

            AssertEdges(graph, obstacle1Coord1, intersection_O1C1_O2C1);
            // Cannot be tested properly due to multiple nodes at the location: AssertEdges(graph, intersection_O1C1_O2C1, obstacle2Coord1);

            AssertEdges(graph, obstacle1Coord1, intersection_O1C1_O2C2);
            AssertEdges(graph, intersection_O1C1_O2C2, obstacle2Coord2);

            // Nodes and edges in road 1
            AssertEdges(graph, (-1, 1.5), (0.25, 1.5));
            AssertEdges(graph, (0.25, 1.5), (0.75, 1.5));
            AssertEdges(graph, (0.75, 1.5), (1, 1.5));
            AssertEdges(graph, (1, 1.5), (1.5, 1.5));
            AssertEdges(graph, (1.5, 1.5), (1.75, 1.5));
            AssertEdges(graph, (1.75, 1.5), (3, 1.5));

            // Nodes and edges in road 2
            AssertEdges(graph, (1.5, 1.5), (1.5, 1));
            AssertEdges(graph, (1.5, 1), (1.5, -1));
        }

        [Test]
        public void RouteOnRoad()
        {
            var shortestPath = hybridVisibilityGraph.ShortestPath(new Position(-1, 1.5), new Position(3, 1.5));

            CollectionAssert.AreEqual(
                new List<Position>
                {
                    new(-1, 1.5),
                    new(0.25, 1.5),
                    new(0.75, 1.5),
                    new(1, 1.5),
                    new(1.5, 1.5),
                    new(1.75, 1.5),
                    new(3, 1.5)
                },
                shortestPath
            );
        }

        [Test]
        public void RouteOnVisibilityEdges()
        {
            var shortestPath = hybridVisibilityGraph.ShortestPath(new Position(0.5, 2), new Position(1, 0));

            CollectionAssert.AreEqual(
                new List<Position>
                {
                    new(0.5, 2),
                    new(0.75, 1.5),
                    new(1, 1),
                    new(1, 0)
                },
                shortestPath
            );
        }

        [Test]
        public void RouteOnRoadEndVisibilityEdges()
        {
            var shortestPath = hybridVisibilityGraph.ShortestPath(new Position(-1, 1.5), new Position(1, 0));

            CollectionAssert.AreEqual(
                new List<Position>
                {
                    new(-1, 1.5),
                    new(0.25, 1.5),
                    new(0, 1),
                    new(1, 0)
                },
                shortestPath
            );
        }

        [Test]
        public void RouteBetweenArbitraryLocations_short()
        {
            var shortestPath = hybridVisibilityGraph.ShortestPath(new Position(0, 1.75), new Position(1.25, 0.5));

            CollectionAssert.AreEqual(
                new List<Position>
                {
                    new(0, 1.75),
                    new(1, 1),
                    new(1.25, 0.5)
                },
                shortestPath
            );
        }

        [Test]
        public void RouteBetweenArbitraryLocations_long()
        {
            var shortestPath = hybridVisibilityGraph.ShortestPath(new Position(0.75, 0.75), new Position(1.5, 2.25));

            CollectionAssert.AreEqual(
                new List<Position>
                {
                    new(0.75, 0.75),
                    new(0, 1),
                    new(0.25, 1.5),
                    new(0.5, 2),
                    new(1.5, 2.25)
                },
                shortestPath
            );
        }
    }

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
        Assert.AreEqual(obstacle.Coordinates[0], obstacle.Vertices[0].Coordinate);
        Assert.AreEqual(obstacle.Coordinates[1], obstacle.Vertices[1].Coordinate);
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
        var obstacles = ObstacleTestHelper.CreateObstacles(new LineString(new[]
            {
                new Coordinate(0, 0),
                new Coordinate(1, 0),
                new Coordinate(2, 0)
            }),
            new LineString(new[]
            {
                new Coordinate(0.5, 1),
                new Coordinate(10, 1)
            }),
            new LineString(new[]
            {
                new Coordinate(0, 2),
                new Coordinate(1, 2),
                new Coordinate(2, 2)
            })
        );
        var obstacle1 = obstacles[0];
        var obstacle2 = obstacles[1];
        var obstacle3 = obstacles[2];

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
        var obstacle = ObstacleTestHelper.CreateObstacle(new Polygon(new LinearRing(new[]
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
        var bottomVertex = new Vertex(new Coordinate(1, 0));
        // Middle line with tree vertices
        var middleVertex0 = new Vertex(new Coordinate(0, 1), new[] { new Position(1, 1) });
        var middleVertex1 = new Vertex(new Coordinate(1, 1), new[] { new Position(0, 1), new Position(2, 1) });
        var middleVertex2 = new Vertex(new Coordinate(2, 1), new[] { new Position(1, 1) });
        // Top line with two vertices
        var topVertex0 = new Vertex(new Coordinate(0, 2), new[] { new Position(1, 2) });
        var topVertex1 = new Vertex(new Coordinate(1, 2), new[] { new Position(0, 2) });

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
                if (nodeData.Position.DistanceInMTo(vertex.Coordinate.ToPosition()) < 0.001)
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

        AssertEdges(spatialGraph, node, vertexToNode[middleVertex0][0]);
        AssertEdges(spatialGraph, node, vertexToNode[middleVertex0][0]);
        AssertEdges(spatialGraph, node, middleVertex1NodeBottom);
        AssertEdges(spatialGraph, node, vertexToNode[middleVertex2][0]);

        // middleVertex0
        nodes = vertexToNode[middleVertex0];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(10, edges.Count);

        AssertEdges(spatialGraph, node, vertexToNode[bottomVertex][0]);
        AssertEdges(spatialGraph, node, middleVertex1NodeTop);
        AssertEdges(spatialGraph, node, middleVertex1NodeBottom);
        AssertEdges(spatialGraph, node, vertexToNode[topVertex1][0]);
        AssertEdges(spatialGraph, node, vertexToNode[topVertex0][0]);

        // middleVertex1
        nodes = vertexToNode[middleVertex1];
        Assert.AreEqual(2, nodes.Count);

        // middleVertex1 - top node
        node = middleVertex1NodeTop;
        edges = spatialGraph.Edges.Values
            .Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(8, edges.Count);

        AssertEdges(spatialGraph, node, vertexToNode[middleVertex0][0]);
        AssertEdges(spatialGraph, node, vertexToNode[topVertex0][0]);
        AssertEdges(spatialGraph, node, vertexToNode[topVertex1][0]);
        AssertEdges(spatialGraph, node, vertexToNode[middleVertex2][0]);

        // middleVertex1 - bottom node
        node = middleVertex1NodeBottom;
        edges = spatialGraph.Edges.Values
            .Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(6, edges.Count);

        AssertEdges(spatialGraph, node, vertexToNode[middleVertex0][0]);
        AssertEdges(spatialGraph, node, vertexToNode[bottomVertex][0]);
        AssertEdges(spatialGraph, node, vertexToNode[middleVertex2][0]);

        // middleVertex2
        nodes = vertexToNode[middleVertex2];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(10, edges.Count);

        AssertEdges(spatialGraph, node, vertexToNode[bottomVertex][0]);
        AssertEdges(spatialGraph, node, middleVertex1NodeTop);
        AssertEdges(spatialGraph, node, middleVertex1NodeBottom);
        AssertEdges(spatialGraph, node, vertexToNode[topVertex1][0]);
        AssertEdges(spatialGraph, node, vertexToNode[topVertex0][0]);

        // topVertex0
        nodes = vertexToNode[topVertex0];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(8, edges.Count);

        AssertEdges(spatialGraph, node, vertexToNode[middleVertex0][0]);
        AssertEdges(spatialGraph, node, middleVertex1NodeTop);
        AssertEdges(spatialGraph, node, vertexToNode[middleVertex2][0]);

        // topVertex1
        nodes = vertexToNode[topVertex1];
        Assert.AreEqual(1, nodes.Count);
        node = nodes[0];
        edges = spatialGraph.Edges.Values.Where(edge => edge.From == node || edge.To == node).ToList();
        Assert.AreEqual(8, edges.Count);

        AssertEdges(spatialGraph, node, vertexToNode[middleVertex0][0]);
        AssertEdges(spatialGraph, node, middleVertex1NodeTop);
        AssertEdges(spatialGraph, node, vertexToNode[middleVertex2][0]);
    }

    [Test]
    public void AddAttributesToPoiNodes()
    {
        // Arrange
        var poiFeature1 = new Feature(
            new Point(1, 1),
            new AttributesTable(new Dictionary<string, object>
            {
                { "poi", "foo" }
            })
        );
        var noPoiFeature = new Feature(
            new Point(2, 2),
            new AttributesTable(new Dictionary<string, object>
            {
                { "not-a-poi", "bar" }
            })
        );
        var poiFeature2 = new Feature(
            new Point(3, 3),
            new AttributesTable(new Dictionary<string, object>
            {
                { "some", "other attribute" },
                { "poi", "blubb" }
            })
        );
        var poiFeature3 = new Feature(
            new Point(4, 4),
            new AttributesTable(new Dictionary<string, object>
            {
                { "poi", "lorem ipsum" }
            })
        );

        var features = new List<Feature>
        {
            poiFeature1,
            noPoiFeature,
            poiFeature2,
        };

        var graph = new SpatialGraph();
        features.Each(f => graph.AddNode(f.Geometry.Coordinates[0].X, f.Geometry.Coordinates[0].Y));

        // Add third POI feature which has no node in the graph
        features.Add(poiFeature3);

        // Act
        HybridVisibilityGraphGenerator.AddAttributesToPoiNodes(features, graph);

        // Assert
        Assert.AreEqual(3, graph.NodesMap.Count);

        Assert.AreEqual(poiFeature1.Geometry.Coordinates[0], graph.NodesMap[0].Position.ToCoordinate());
        CollectionAssert.AreEquivalent(poiFeature1.Attributes.ToObjectDictionary(), graph.NodesMap[0].Data);

        Assert.AreEqual(noPoiFeature.Geometry.Coordinates[0], graph.NodesMap[1].Position.ToCoordinate());
        CollectionAssert.IsEmpty(graph.NodesMap[1].Data);

        Assert.AreEqual(poiFeature2.Geometry.Coordinates[0], graph.NodesMap[2].Position.ToCoordinate());
        CollectionAssert.AreEquivalent(poiFeature2.Attributes.ToObjectDictionary(), graph.NodesMap[2].Data);
    }

    [Test]
    public void MergeRoadsIntoGraph()
    {
        var graph = new SpatialGraph();

        var node00 = graph.AddNode(0, 0);
        var node01 = graph.AddNode(0, 1);
        var node10 = graph.AddNode(1, 0);
        var node11 = graph.AddNode(1, 1);

        // Directional edge
        graph.AddEdge(node00, node01);
        // Bi-directional edge
        graph.AddEdge(node10, node11);
        graph.AddEdge(node11, node10);

        var features = new[]
        {
            // non-road feature
            new Feature(
                new LineString(new[]
                {
                    new Coordinate(-2, 0.25),
                    new Coordinate(2, 0.25)
                }),
                new AttributesTable(
                    new Dictionary<string, object> { { "foo", "bar" } }
                )
            ),
            // road feature
            new Feature(
                new LineString(new[]
                {
                    new Coordinate(-2, 0.5),
                    new Coordinate(2, 0.5)
                }),
                new AttributesTable(
                    new Dictionary<string, object> { { "highway", "road" } }
                )
            )
        };

        // Act
        HybridVisibilityGraphGenerator.MergeRoadsIntoGraph(features, graph);

        // Assert
        Assert.AreEqual(8, graph.NodesMap.Count);
        var nodePositions = graph.NodesMap.Map(pair => pair.Value.Position);

        // Left vertical line
        CollectionAssert.Contains(nodePositions, new Position(0, 0));
        CollectionAssert.Contains(nodePositions, new Position(0, 1));

        // Right vertical line
        CollectionAssert.Contains(nodePositions, new Position(1, 0));
        CollectionAssert.Contains(nodePositions, new Position(1, 1));

        // Road segment
        CollectionAssert.Contains(nodePositions, new Position(-2, 0.5));
        CollectionAssert.Contains(nodePositions, new Position(0, 0.5));
        CollectionAssert.Contains(nodePositions, new Position(1, 0.5));
        CollectionAssert.Contains(nodePositions, new Position(2, 0.5));

        var edges = graph.Edges.Map(pair => pair.Value.Geometry);

        // New bi-directional edges for the added road
        AssertEdges(graph, (-2, 0.5), (0, 0.5));
        AssertEdges(graph, (0, 0.5), (-2, 0.5));

        AssertEdges(graph, (0, 0.5), (1, 0.5));
        AssertEdges(graph, (1, 0.5), (0, 0.5));

        AssertEdges(graph, (1, 0.5), (2, 0.5));
        AssertEdges(graph, (2, 0.5), (1, 0.5));

        // New edges for the former left edge
        AssertEdge(graph, (0, 0), (0, 0.5));
        AssertEdge(graph, (0, 0.5), (0, 1));

        // New edges for the former right edges
        AssertEdges(graph, (1, 0), (1, 0.5));
        AssertEdges(graph, (1, 0.5), (1, 1));
        AssertEdges(graph, (1, 1), (1, 0.5));
        AssertEdges(graph, (1, 0.5), (1, 0));

        Assert.AreEqual(12, graph.Edges.Count);

        // Original edges should not exist anymore
        CollectionAssert.DoesNotContain(edges, new[] { new Position(0, 0), new Position(0, 1) });
        CollectionAssert.DoesNotContain(edges, new[] { new Position(1, 0), new Position(1, 1) });
        CollectionAssert.DoesNotContain(edges, new[] { new Position(1, 1), new Position(1, 0) });
    }

    private void AssertEdges(SpatialGraph graph, (double, double) coordinateA, (double, double) coordinateB)
    {
        AssertEdges(
            graph,
            GetNode(graph, new Coordinate(coordinateA.Item1, coordinateA.Item2)),
            GetNode(graph, new Coordinate(coordinateB.Item1, coordinateB.Item2))
        );
    }

    private void AssertEdge(SpatialGraph graph, (double, double) coordinateA, (double, double) coordinateB)
    {
        AssertEdge(
            graph,
            GetNode(graph, new Coordinate(coordinateA.Item1, coordinateA.Item2)),
            GetNode(graph, new Coordinate(coordinateB.Item1, coordinateB.Item2))
        );
    }

    private static void AssertEdges(SpatialGraph graph, int nodeA, int nodeB)
    {
        AssertEdge(graph, nodeA, nodeB);
        AssertEdge(graph, nodeB, nodeA);
    }

    private static void AssertEdge(SpatialGraph graph, int nodeA, int nodeB)
    {
        CollectionAssert.Contains(graph.EdgesMap.Keys, (nodeA, nodeB));
    }

    private int GetNode(ISpatialGraph graph, Coordinate coordinate)
    {
        var nodes = graph.NodesMap
            .Map(pair => pair.Value)
            .Where(node => node.Position.DistanceInMTo(coordinate.ToPosition()) < 0.0001)
            .ToList();

        return nodes.Any() ? nodes.First().Key : -1;
    }
}