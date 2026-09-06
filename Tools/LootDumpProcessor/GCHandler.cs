namespace LootDumpProcessor;

public static class GCHandler
{
    public static void Collect()
    {
        if (LootDumpProcessorContext.GetConfig().ManualGarbageCollectionCalls)
        {
            GC.Collect();
        }
    }
}
