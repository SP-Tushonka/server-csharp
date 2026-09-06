using LootDumpProcessor.Model.Processing;
using LootDumpProcessor.Serializers.Json;

namespace LootDumpProcessor.Process.Collector;

public class DumpCollector : ICollector
{
    private static readonly IJsonSerializer _jsonSerializer = new NetJsonSerializer();
    private static readonly string DumpLocation =
        Path.Combine(LootDumpProcessorContext.CollectorTempDirectory, "collector") + Path.DirectorySeparatorChar;
    private readonly List<PartialData> processedDumps = new(
        LootDumpProcessorContext.GetConfig().CollectorConfig.MaxEntitiesBeforeDumping + 50
    );
    private readonly object lockObject = new();

    public void Setup()
    {
        if (Directory.Exists(DumpLocation))
        {
            Directory.Delete(DumpLocation, true);
        }

        Directory.CreateDirectory(DumpLocation);
    }

    public void Hold(PartialData parsedDump)
    {
        lock (lockObject)
        {
            processedDumps.Add(parsedDump);
            if (processedDumps.Count > LootDumpProcessorContext.GetConfig().CollectorConfig.MaxEntitiesBeforeDumping)
            {
                var fileName = $"collector-{DateTime.Now.ToString("yyyyMMddHHmmssfffff")}.json";
                File.WriteAllText($"{DumpLocation}{fileName}", _jsonSerializer.Serialize(processedDumps));
                processedDumps.Clear();
            }
        }
    }

    public List<PartialData> Retrieve()
    {
        foreach (var file in Directory.GetFiles(DumpLocation))
        {
            processedDumps.AddRange(_jsonSerializer.Deserialize<List<PartialData>>(File.ReadAllText(file)));
        }

        return processedDumps;
    }

    public void Clear()
    {
        lock (lockObject)
        {
            foreach (var file in Directory.GetFiles(DumpLocation))
            {
                File.Delete(file);
            }
            processedDumps.Clear();
        }
    }
}
