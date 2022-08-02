using Mars.Interfaces.Environments;
using NUnit.Framework;

namespace Wavefront.Tests;

public class WavefrontPreprocessorTest
{
    public class TrajectoryCollidesWithObstacle : WavefrontTestHelper.WithWavefrontAlgorithm
    {
        [Test]
        public void NotVisible_BehindSimpleObstacle()
        {
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(3, 7),
                Position.CreateGeoPosition(1, 7)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(3, 0),
                Position.CreateGeoPosition(1.5, 11)));

            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(1, 7),
                Position.CreateGeoPosition(3, 7)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(1.5, 11),
                Position.CreateGeoPosition(3, 0)));
        }

        [Test]
        public void NotVisible_BehindMultiVertexObstacle()
        {
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(8, 3.5),
                Position.CreateGeoPosition(6, 3.5)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(6.5, 2),
                Position.CreateGeoPosition(6.5, 4)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(8, 5),
                Position.CreateGeoPosition(5, 0)));

            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(6, 3.5),
                Position.CreateGeoPosition(8, 3.5)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(6.5, 4),
                Position.CreateGeoPosition(6.5, 2)));
            Assert.True(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(5, 0),
                Position.CreateGeoPosition(8, 5)));
        }

        [Test]
        public void Visible()
        {
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(1, 1),
                Position.CreateGeoPosition(2, 2)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(4, 7),
                Position.CreateGeoPosition(3, 7)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(3, 6),
                Position.CreateGeoPosition(3, 7)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(2, 5),
                Position.CreateGeoPosition(3, 7)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(7, 5),
                Position.CreateGeoPosition(6.5, 3.5)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(3, 5),
                Position.CreateGeoPosition(1, 5)));
            Assert.False(WavefrontPreprocessor.TrajectoryCollidesWithObstacle(obstacles, Position.CreateGeoPosition(1, 5),
                Position.CreateGeoPosition(3, 5)));
        }
    }
}