namespace NukeLearning;

public class Logger : ILogger
{
    public void WriteLine(string? value)
    {
        Console.WriteLine(value);
    }
}
