namespace LootDumpProcessor.Process.Reader.Filters;

public static class FileFilterFactory
{
    private static readonly Dictionary<FileFilterTypes, IFileFilter> _fileFilters = new();
    private static object lockObject = new();

    public static IFileFilter GetInstance(FileFilterTypes type)
    {
        lock (lockObject)
        {
            if (!_fileFilters.TryGetValue(type, out var filter))
            {
                filter = type switch
                {
                    FileFilterTypes.JsonDump => new JsonDumpFileFilter(),
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                };
                _fileFilters.Add(type, filter);
            }

            return filter;
        }
    }
}
