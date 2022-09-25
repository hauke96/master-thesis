using System.Collections.Generic;
using System.Linq;
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
        var vertex = new Vertex(Position.CreateGeoPosition(1, 1), neighborPositions.ToList());

        var neighbor = vertex.RightNeighbor(Position.CreateGeoPosition(2, 2));
        Assert.AreEqual(neighborPositions[0], neighbor);
        
        neighbor = vertex.RightNeighbor(Position.CreateGeoPosition(2, 0));
        Assert.AreEqual(neighborPositions[1], neighbor);
        
        neighbor = vertex.RightNeighbor(Position.CreateGeoPosition(0, 0));
        Assert.AreEqual(neighborPositions[2], neighbor);
        
        neighbor = vertex.RightNeighbor(Position.CreateGeoPosition(0, 2));
        Assert.AreEqual(neighborPositions[0], neighbor);
        
        neighbor = vertex.RightNeighbor(Position.CreateGeoPosition(0, 1));
        Assert.AreEqual(neighborPositions[0], neighbor);

        neighbor = vertex.RightNeighbor(Position.CreateGeoPosition(0, 1), true);
        Assert.AreEqual(neighborPositions[2], neighbor);
    }

    [Test]
    public void GetLeftNeighbor()
    {
        var neighborPositions = new List<Position>();
        neighborPositions.Add(Position.CreateGeoPosition(2, 1)); // right
        neighborPositions.Add(Position.CreateGeoPosition(1, 0)); // below
        neighborPositions.Add(Position.CreateGeoPosition(0, 1)); // left
        var vertex = new Vertex(Position.CreateGeoPosition(1, 1), neighborPositions.ToList());

        var neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(2, 2));
        Assert.AreEqual(neighborPositions[2], neighbor);
        
        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(2, 0));
        Assert.AreEqual(neighborPositions[0], neighbor);
        
        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(0, 0));
        Assert.AreEqual(neighborPositions[1], neighbor);
        
        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(0, 2));
        Assert.AreEqual(neighborPositions[2], neighbor);
        
        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(0, 1));
        Assert.AreEqual(neighborPositions[1], neighbor);
        
        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(1, 0));
        Assert.AreEqual(neighborPositions[0], neighbor);
        
        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(2, 1));
        Assert.AreEqual(neighborPositions[2], neighbor);

        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(2, 1), true);
        Assert.AreEqual(neighborPositions[0], neighbor);

        // Additional test when no rotation happens internally
        neighborPositions = new List<Position>();
        neighborPositions.Add(Position.CreateGeoPosition(1, 2)); // above
        neighborPositions.Add(Position.CreateGeoPosition(1, 0)); // below
        neighborPositions.Add(Position.CreateGeoPosition(0, 1)); // left
        vertex = new Vertex(Position.CreateGeoPosition(1, 1), neighborPositions.ToList());

        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(1, 2));
        Assert.AreEqual(neighborPositions[2], neighbor);

        neighbor = vertex.LeftNeighbor(Position.CreateGeoPosition(1, 2), true);
        Assert.AreEqual(neighborPositions[0], neighbor);
    }
}