namespace LootDumpProcessor.Process.Processor.DumpProcessor;

public static class DumpProcessorFactory
{
    public static IDumpProcessor GetInstance()
    {
        // Implement real factory
        return new MultithreadSteppedDumpProcessor();
    }
}
