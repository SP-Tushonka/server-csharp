namespace LootDumpProcessor.Logger;

public static class LoggerFactory
{
    private static ILogger? _logger;

    public static ILogger GetInstance()
    {
        if (_logger == null)
            _logger = new QueueLogger();
        // TODO: implement factory
        return _logger;
    }
}
