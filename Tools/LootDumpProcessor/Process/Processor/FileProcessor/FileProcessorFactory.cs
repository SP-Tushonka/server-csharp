namespace LootDumpProcessor.Process.Processor.FileProcessor;

public static class FileProcessorFactory
{
    private static IFileProcessor? _fileProcessor;

    public static IFileProcessor GetInstance()
    {
        // TODO: implement actual factory someday
        if (_fileProcessor == null)
            _fileProcessor = new FileProcessor();
        return _fileProcessor;
    }
}
