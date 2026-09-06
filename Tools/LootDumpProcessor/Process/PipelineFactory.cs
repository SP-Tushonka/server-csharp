namespace LootDumpProcessor.Process;

public static class PipelineFactory
{
    public static IPipeline GetInstance()
    {
        // implement actual factory at some point
        return new QueuePipeline();
    }
}
