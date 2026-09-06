using System;

namespace QuestValidator.Common.Helpers
{
    public static class LoggingHelpers
    {
        public static string LogTimeTaken(double totalSeconds)
        {
            return Math.Round(totalSeconds, 2, MidpointRounding.ToEven).ToString();
        }

        public static void LogWarning(string message)
        {
            Console.BackgroundColor = ConsoleColor.DarkYellow;
            Console.ForegroundColor = ConsoleColor.Black;

            Console.Write(message);
            ResetConsoleColours();
            Console.WriteLine();
        }

        public static void LogInfo(string message)
        {
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;

            Console.Write(message);
            ResetConsoleColours();
            Console.WriteLine();
        }

        public static void LogSuccess(string message)
        {
            Console.BackgroundColor = ConsoleColor.Green;
            Console.ForegroundColor = ConsoleColor.Black;

            Console.Write(message);
            ResetConsoleColours();
            Console.WriteLine();
        }

        public static void LogError(string message)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;

            Console.Write(message);
            ResetConsoleColours();
            Console.WriteLine();
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
}
