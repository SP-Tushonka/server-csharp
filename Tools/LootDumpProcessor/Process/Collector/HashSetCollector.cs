using LootDumpProcessor.Model.Processing;

namespace LootDumpProcessor.Process.Collector;

public class HashSetCollector : ICollector
{
    private readonly HashSet<PartialData> processedDumps = new(100_000);
    private readonly object lockObject = new();

    public void Setup() { }

    public void Hold(PartialData outputData)
    {
        lock (lockObject)
        {
            processedDumps.Add(outputData);
        }
    }

    public List<PartialData> Retrieve()
    {
        return processedDumps.ToList();
    }

    public void Clear()
    {
        processedDumps.Clear();
    }
}
