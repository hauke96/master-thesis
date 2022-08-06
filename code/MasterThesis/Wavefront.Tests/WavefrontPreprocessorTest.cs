using NetTopologySuite.Geometries;
using NUnit.Framework;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Tests;

public class WavefrontPreprocessorTest
{
    public class TrajectoryCollidesWithObstacle : WavefrontTestHelper.WithWavefrontAlgorithm
    {
        [Test]
        public void NotVisible_BehindSimpleObstacle()
        {
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(3, 7),
                new Coordinate(1, 7)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(3, 0),
                new Coordinate(1.5, 11)));

            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(1, 7),
                new Coordinate(3, 7)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(1.5, 11),
                new Coordinate(3, 0)));
        }

        [Test]
        public void NotVisible_BehindMultiVertexObstacle()
        {
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(8, 3.5),
                new Coordinate(6, 3.5)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(6.5, 2),
                new Coordinate(6.5, 4)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(8, 5),
                new Coordinate(5, 0)));

            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(6, 3.5),
                new Coordinate(8, 3.5)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(6.5, 4),
                new Coordinate(6.5, 2)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(5, 0),
                new Coordinate(8, 5)));
        }

        [Test]
        public void Visible()
        {
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(1, 1),
                new Coordinate(2, 2)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(4, 7),
                new Coordinate(3, 7)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(3, 6),
                new Coordinate(3, 7)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(2, 5),
                new Coordinate(3, 7)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(7, 5),
                new Coordinate(6.5, 3.5)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(3, 5),
                new Coordinate(1, 5)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, new Coordinate(1, 5),
                new Coordinate(3, 5)));
        }
    }
}