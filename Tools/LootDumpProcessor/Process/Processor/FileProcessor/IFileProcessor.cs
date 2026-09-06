using LootDumpProcessor.Model.Processing;

namespace LootDumpProcessor.Process.Processor.FileProcessor;

public interface IFileProcessor
{
    PartialData Process(BasicInfo parsedData);
}
