namespace HybridVisibilityGraphRouting;

public class Log
{
    public static readonly int DEBUG = 0;
    public static readonly int INFO = 1;

    public static int LogLevel = INFO;

    public static void I(string message)
    {
        Console.WriteLine(message);
    }

    public static void D(string message)
    {
        if (LogLevel <= DEBUG)
        {
            I(message);
        }
    }

    public static void Note(string message)
    {
        D("- " + message);
    }
}