namespace Common;

public static class LoggingHelpers
{
    public static string LogTimeTaken(double totalSeconds)
    {
        return Math.Round(totalSeconds, 2, MidpointRounding.ToEven).ToString();
    }

    public static void LogToConsole(string message, ConsoleColor backgroundColour = ConsoleColor.Green)
    {
        Console.BackgroundColor = backgroundColour;
        Console.ForegroundColor = ConsoleColor.Black;

        Console.Write(message);
        ResetConsoleColours();
        Console.WriteLine();
    }

    private static void ResetConsoleColours()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }
}
