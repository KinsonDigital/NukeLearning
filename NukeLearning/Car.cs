namespace NukeLearning;

public class Car
{
    private readonly ILogger logger;

    public Car(ILogger logger)
    {
        this.logger = logger;
    }

    public void TurnOn()
    {
        this.logger.WriteLine("Car Turned On.");
    }
}
