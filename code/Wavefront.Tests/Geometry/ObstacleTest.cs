using System.Collections.Generic;
using Mars.Common;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront.Tests.Geometry;

public class ObstacleTest
{
    [Test]
    public void GetAngleAreaOfObstacle_Vertical()
    {
        var obstacle = new Obstacle(new LineString(new[]
        {
            new Coordinate(1, 1),
            new Coordinate(1, 3)
        }));

        // From below the obstacle
        var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(0, 0));
        Assert.AreEqual(18.434, angleFrom, 0.001);
        Assert.AreEqual(45, angleTo, 0.001);
        Assert.AreEqual(3.1622, distance, 0.001);

        // From above the obstacle
        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(0, 4));
        Assert.AreEqual(135, angleFrom, 0.001);
        Assert.AreEqual(161.566, angleTo, 0.001);
        Assert.AreEqual(3.1622, distance, 0.001);

        // From lower coordinate of the obstacle
        (angleFrom, angleTo, distance) =
            obstacle.GetAngleAreaOfObstacle(new Vertex(obstacle.Coordinates[0].ToPosition()));
        Assert.AreEqual(0, angleFrom, 0.001);
        Assert.AreEqual(0, angleTo, 0.001);
        Assert.AreEqual(2, distance, 0.001);
    }

    [Test]
    public void GetAngleAreaOfObstacle_Horizontal()
    {
        var obstacle = new Obstacle(new LineString(new[]
        {
            new Coordinate(1, 1),
            new Coordinate(3, 1)
        }));

        // From below the obstacle
        var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(0, 0));
        Assert.AreEqual(45, angleFrom, 0.001);
        Assert.AreEqual(71.565, angleTo, 0.001);
        Assert.AreEqual(3.1622, distance, 0.001);

        // From above the obstacle
        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(4, 0));
        Assert.AreEqual(288.434, angleFrom, 0.001);
        Assert.AreEqual(315, angleTo, 0.001);
        Assert.AreEqual(3.1622, distance, 0.001);

        // From above the obstacle
        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(2, 0));
        Assert.AreEqual(315, angleFrom, 0.001);
        Assert.AreEqual(45, angleTo, 0.001);
        Assert.AreEqual(1.414, distance, 0.001);
    }

    [Test]
    public void GetAngleAreaOfObstacle_VertexOfObstacle()
    {
        // V-shape obstacle
        var obstacle = new Obstacle(new LineString(new[]
        {
            new Coordinate(1, 2),
            new Coordinate(2, 1),
            new Coordinate(3, 2)
        }));

        // Get angle area of the tip of the "V".
        var (angleFrom, angleTo, distance) =
            obstacle.GetAngleAreaOfObstacle(new Vertex(obstacle.Coordinates[1].ToPosition()));

        Assert.AreEqual(315, angleFrom, 0.001);
        Assert.AreEqual(45, angleTo, 0.001);
        Assert.AreEqual(1.414, distance, 0.001);
    }

    [Test]
    public void GetAngleAreaOfObstacle_ClosedObstacle()
    {
        var obstacle = new Obstacle(new LineString(new[]
        {
            new Coordinate(1, 1),
            new Coordinate(2, 1),
            new Coordinate(2, 2),
            new Coordinate(1, 2),
            new Coordinate(1, 1),
        }));

        double angleFrom;
        double angleTo;
        double distance;

        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(0, 0));
        Assert.AreEqual(26.565, angleFrom, 0.001);
        Assert.AreEqual(63.434, angleTo, 0.001);
        Assert.AreEqual(2.236, distance, 0.001);

        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(1.5, 0));
        Assert.AreEqual(333.434, angleFrom, 0.001);
        Assert.AreEqual(26.565, angleTo, 0.001);
        Assert.AreEqual(1.118, distance, 0.001);

        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(1, 1));
        Assert.AreEqual(0, angleFrom, 0.001);
        Assert.AreEqual(90, angleTo, 0.001);
        Assert.AreEqual(1, distance, 0.001);

        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(2, 1));
        Assert.AreEqual(270, angleFrom, 0.001);
        Assert.AreEqual(0, angleTo, 0.001);
        Assert.AreEqual(1, distance, 0.001);

        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(2, 2));
        Assert.AreEqual(180, angleFrom, 0.001);
        Assert.AreEqual(270, angleTo, 0.001);
        Assert.AreEqual(1, distance, 0.001);

        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(1, 2));
        Assert.AreEqual(90, angleFrom, 0.001);
        Assert.AreEqual(180, angleTo, 0.001);
        Assert.AreEqual(1, distance, 0.001);
    }

    [Test]
    public void IntersectsWithLine_convexPolygon()
    {
        // square
        var coordinates = new[]
        {
            new Coordinate(1, 1),
            new Coordinate(2, 1),
            new Coordinate(2, 2),
            new Coordinate(1, 2),
            new Coordinate(1, 1),
        };
        var obstacle = new Obstacle(new LineString(coordinates));
        var coordinateToObstacles = WavefrontPreprocessor.GetCoordinateToObstaclesMapping(new List<Obstacle> { obstacle });

        // Edges on outside of polygon
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], coordinates[1], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[1], coordinates[2], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[2], coordinates[3], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[3], coordinates[0], coordinateToObstacles));

        // Edges between corners of polygon
        // TODO
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[0], coordinates[2], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[1], coordinates[3], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[2], coordinates[0], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[3], coordinates[1], coordinateToObstacles));

        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(1.5, 0), new Coordinate(1.5, 3), coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(0, 1.5), new Coordinate(3, 1.5), coordinateToObstacles));

        // collinear vertices
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0],
            new Coordinate(coordinates[1].X + 1, coordinates[0].Y), coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0],
            new Coordinate(coordinates[1].X + 10, coordinates[0].Y), coordinateToObstacles));

        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0],
            new Coordinate(coordinates[3].X, coordinates[3].Y + 1), coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0],
            new Coordinate(coordinates[3].X, coordinates[3].Y + 10), coordinateToObstacles));
        
        // Line from an vertex to the inside
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[0], new Coordinate(1.5, 1.5), coordinateToObstacles));
        // Line from an vertex somewhere to the outside
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], new Coordinate(-1.5, -1.5), coordinateToObstacles));
        // Line completely within
        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(1.25, 1.5), new Coordinate(1.75, 1.5), coordinateToObstacles));
    }
    
    [Test]
    public void IntersectsWithLine_lineObstacle()
    {
        // square
        var coordinates = new[]
        {
            new Coordinate(1, 1),
            new Coordinate(2, 1),
            new Coordinate(2, 2),
        };
        var obstacle = new Obstacle(new LineString(coordinates));
        var coordinateToObstacles = WavefrontPreprocessor.GetCoordinateToObstaclesMapping(new List<Obstacle> { obstacle });

        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], coordinates[1], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], coordinates[2], coordinateToObstacles));
        
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[1], coordinates[0], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[1], coordinates[2], coordinateToObstacles));
        
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[2], coordinates[0], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[2], coordinates[1], coordinateToObstacles));

        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(1.5, 0), new Coordinate(1.5, 3), coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(0, 1.5), new Coordinate(3, 1.5), coordinateToObstacles));
        
        // Line from an vertex somewhere to the outside
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], new Coordinate(-0.5, -0.5), coordinateToObstacles));
    }
    
    [Test]
    public void IntersectsWithLine_concavePolygon()
    {
        /*
         * Pacman like polygon:
         *  _______
         * |    .`
         * |   `.
         * |_____`.
         */
        var coordinates = new[]
        {
            new Coordinate(0,0),
            new Coordinate(2,0),
            new Coordinate(1,1),
            new Coordinate(2,2),
            new Coordinate(0,2),
            new Coordinate(0,0),
        };
        var obstacle = new Obstacle(new LineString(coordinates));
        var coordinateToObstacles = WavefrontPreprocessor.GetCoordinateToObstaclesMapping(new List<Obstacle> { obstacle });

        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], coordinates[1], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[0], coordinates[2], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[0], coordinates[3], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], coordinates[4], coordinateToObstacles));

        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[1], coordinates[0], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[1], coordinates[2], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[1], coordinates[3], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[1], coordinates[4], coordinateToObstacles));

        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[2], coordinates[0], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[2], coordinates[1], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[2], coordinates[3], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[2], coordinates[4], coordinateToObstacles));

        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[3], coordinates[0], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[3], coordinates[1], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[3], coordinates[2], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[3], coordinates[4], coordinateToObstacles));

        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[4], coordinates[0], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[4], coordinates[1], coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[4], coordinates[2], coordinateToObstacles));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[4], coordinates[3], coordinateToObstacles));

        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(0.5, 0), new Coordinate(0.5, 3), coordinateToObstacles));
        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(0, 0.5), new Coordinate(3, 0.5), coordinateToObstacles));
        
        // Line from an vertex to the inside
        Assert.IsTrue(obstacle.IntersectsWithLine(coordinates[0], new Coordinate(0.5, 0.5), coordinateToObstacles));
        // Line from an vertex somewhere to the outside
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], new Coordinate(-0.5, -0.5), coordinateToObstacles));
        // Line completely within
        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(0.25, 0.5), new Coordinate(0.75, 0.5), coordinateToObstacles));
    }
}