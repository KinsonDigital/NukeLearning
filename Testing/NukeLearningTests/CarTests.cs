using Moq;
using NukeLearning;

namespace NukeLearningTests;

public class CarTests
{
    [Fact]
    public void TurnOn_WhenInvoked_TurnsCarOn()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var car = new Car(mockLogger.Object);

        // Act
        car.TurnOn();

        // Assert
        mockLogger.Verify(m => m.WriteLine("Car Turned Off."));
    }
}
