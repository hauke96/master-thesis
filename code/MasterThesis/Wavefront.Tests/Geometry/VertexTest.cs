using System.Collections.Generic;
using Mars.Interfaces.Environments;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront.Tests.Geometry;

public class VertexTest
{
    [Test]
    public void GetRightNeighbor()
    {
        var neighborPositions = new List<Position>();
        neighborPositions.Add(Position.CreateGeoPosition(2, 1)); // right
        neighborPositions.Add(Position.CreateGeoPosition(1, 0)); // below
        neighborPositions.Add(Position.CreateGeoPosition(0, 1)); // left
        var vertex = new Vertex(Position.CreateGeoPosition(1, 1), neighborPositions.ToSet());

        var rightNeighbor = vertex.RightNeighbor(Position.CreateGeoPosition(2, 2));
        Assert.AreEqual(neighborPositions[2], rightNeighbor);

        rightNeighbor = vertex.RightNeighbor(Position.CreateGeoPosition(2, 0));
        Assert.AreEqual(neighborPositions[0], rightNeighbor);

        rightNeighbor = vertex.RightNeighbor(Position.CreateGeoPosition(0, 0));
        Assert.AreEqual(neighborPositions[1], rightNeighbor);

        rightNeighbor = vertex.RightNeighbor(Position.CreateGeoPosition(0, 2));
        Assert.AreEqual(neighborPositions[2], rightNeighbor);
    }
    
    [Test]
    public void GetLeftNeighbor()
    {
        var neighborPositions = new List<Position>();
        neighborPositions.Add(Position.CreateGeoPosition(2, 1)); // right
        neighborPositions.Add(Position.CreateGeoPosition(1, 0)); // below
        neighborPositions.Add(Position.CreateGeoPosition(0, 1)); // left
        var vertex = new Vertex(Position.CreateGeoPosition(1, 1), neighborPositions.ToSet());

        var rightNeighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(2, 2));
        Assert.AreEqual(neighborPositions[0], rightNeighbor);

        rightNeighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(2, 0));
        Assert.AreEqual(neighborPositions[1], rightNeighbor);

        rightNeighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(0, 0));
        Assert.AreEqual(neighborPositions[2], rightNeighbor);

        rightNeighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(0, 2));
        Assert.AreEqual(neighborPositions[0], rightNeighbor);
    }
}