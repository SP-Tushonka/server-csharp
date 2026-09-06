namespace LootDumpProcessor.Process.Collector;

public static class CollectorFactory
{
    private static ICollector? _collector;

    public static ICollector GetInstance()
    {
        if (_collector == null)
            _collector = LootDumpProcessorContext.GetConfig().CollectorConfig.CollectorType switch
            {
                CollectorType.Memory => new HashSetCollector(),
                CollectorType.Dump => new DumpCollector(),
                _ => throw new ArgumentOutOfRangeException(),
            };
        return _collector;
    }
}
