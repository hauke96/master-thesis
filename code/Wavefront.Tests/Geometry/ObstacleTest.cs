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
        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(obstacle.Coordinates[0].ToPosition()));
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
        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(4,0));
        Assert.AreEqual(288.434, angleFrom, 0.001);
        Assert.AreEqual(315, angleTo, 0.001);
        Assert.AreEqual(3.1622, distance, 0.001);

        // From above the obstacle
        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(2,0));
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
            new Coordinate(2,1),
            new Coordinate(3,2)
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

        var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(0, 0));
        Assert.AreEqual(26.565, angleFrom, 0.001);
        Assert.AreEqual(63.434, angleTo, 0.001);
        Assert.AreEqual(2.236, distance, 0.001);

        (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(new Vertex(1.5, 0));
        Assert.AreEqual(333.434, angleFrom, 0.001);
        Assert.AreEqual(26.565, angleTo, 0.001);
        Assert.AreEqual(1.118, distance, 0.001);
    }
}