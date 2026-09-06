using System.Collections.Concurrent;

namespace LootDumpProcessor.Logger;

public class QueueLogger : ILogger
{
    private readonly BlockingCollection<LoggedMessage> queuedMessages = new();
    private Task? loggerThread;
    private bool isRunning;
    private int logLevel;
    private const int LogTerminationTimeoutMs = 1000;
    private const int LogTerminationRetryCount = 3;

    public void Setup()
    {
        SetLogLevel();
        isRunning = true;
        loggerThread = Task.Factory.StartNew(
            () =>
            {
                while (isRunning)
                {
                    while (queuedMessages.TryTake(out var value))
                    {
                        Console.ResetColor();
                        switch (value.LogLevel)
                        {
                            case LogLevel.Error:
                                Console.BackgroundColor = ConsoleColor.Red;
                                Console.ForegroundColor = ConsoleColor.Black;
                                break;
                            case LogLevel.Warning:
                                Console.BackgroundColor = ConsoleColor.Yellow;
                                Console.ForegroundColor = ConsoleColor.Black;
                                break;
                            case LogLevel.Debug:
                                Console.ForegroundColor = ConsoleColor.DarkCyan;
                                break;
                            case LogLevel.Info:
                            default:
                                break;
                        }

                        Console.WriteLine(value.Message);
                    }

                    Thread.Sleep(TimeSpan.FromMilliseconds(LootDumpProcessorContext.GetConfig().LoggerConfig.QueueLoggerPoolingTimeoutMs));
                }
            },
            TaskCreationOptions.LongRunning
        );
    }

    private void SetLogLevel()
    {
        logLevel = GetLogLevel(LootDumpProcessorContext.GetConfig().LoggerConfig.LogLevel);
    }

    private int GetLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Error => 1,
            LogLevel.Warning => 2,
            LogLevel.Info => 3,
            LogLevel.Debug => 4,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    public void Log(string message, LogLevel level)
    {
        if (GetLogLevel(level) <= logLevel)
            queuedMessages.Add(new LoggedMessage { Message = message, LogLevel = level });
    }

    public bool CanBeLogged(LogLevel level)
    {
        return GetLogLevel(level) <= logLevel;
    }

    // Wait for graceful termination of the logging thread
    public void Stop()
    {
        isRunning = false;
        if (loggerThread != null)
        {
            Console.ResetColor();
            var retryCount = 0;
            while (!loggerThread.IsCompleted)
            {
                if (retryCount == LogTerminationRetryCount)
                {
                    Console.WriteLine(
                        $"Logger thread did not terminate by itself after {retryCount} retries. Some log messages may be lost."
                    );
                    break;
                }

                Console.WriteLine($"Waiting {LogTerminationTimeoutMs}ms for logger termination");
                Thread.Sleep(LogTerminationTimeoutMs);
                retryCount++;
            }
        }
    }

    private class LoggedMessage
    {
        public string Message { get; init; }
        public LogLevel LogLevel { get; init; }
    }
}
