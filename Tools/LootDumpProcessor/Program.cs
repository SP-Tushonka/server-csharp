using LootDumpProcessor.Logger;
using LootDumpProcessor.Process;
using LootDumpProcessor.Storage;

namespace LootDumpProcessor;

public static class Program
{
    public static void Main()
    {
        // Bootstrap the config before anything else, its required by the whole application to work
        LootDumpProcessorContext.GetConfig();
        // Some loggers may need a startup and stop mechanism
        LoggerFactory.GetInstance().Setup();
        // Setup Data storage
        DataStorageFactory.GetInstance().Setup();
        // startup the pipeline
        PipelineFactory.GetInstance().DoProcess();
        // stop loggers at the end
        LoggerFactory.GetInstance().Stop();
        Thread.Sleep(10000);
    }
}
