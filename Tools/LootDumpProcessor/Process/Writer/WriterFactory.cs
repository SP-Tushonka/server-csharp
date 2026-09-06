namespace LootDumpProcessor.Process.Writer;

public static class WriterFactory
{
    public static IWriter GetInstance()
    {
        // implement actual factory someday
        return new FileWriter();
    }
}
