namespace LootDumpProcessor.Logger;

public interface ILogger
{
    void Setup();
    void Log(string message, LogLevel level);
    bool CanBeLogged(LogLevel level);
    void Stop();
}
