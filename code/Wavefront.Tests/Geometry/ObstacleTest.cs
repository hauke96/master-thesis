using Mars.Common;
using NetTopologySuite.Geometries;
using NUnit.Framework;
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
    public void IntersectsWithLine()
    {
        var coordinates = new[]
        {
            new Coordinate(1, 1),
            new Coordinate(2, 1),
            new Coordinate(2, 2),
            new Coordinate(1, 2),
            new Coordinate(1, 1),
        };
        var obstacle = new Obstacle(new LineString(coordinates));

        // Edges on outside of polygon
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], coordinates[1]));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[1], coordinates[2]));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[2], coordinates[3]));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[3], coordinates[0]));

        // Edges between corners of polygon
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[0], coordinates[2]));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[1], coordinates[3]));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[2], coordinates[0]));
        Assert.IsFalse(obstacle.IntersectsWithLine(coordinates[3], coordinates[1]));

        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(1.5, 0), new Coordinate(1.5, 3)));
        Assert.IsTrue(obstacle.IntersectsWithLine(new Coordinate(0, 1.5), new Coordinate(3, 1.5)));
    }
}