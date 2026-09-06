namespace LootDumpProcessor.Storage.Implementations.Memory;

public class MemoryDataStorage : IDataStorage
{
    private static readonly Dictionary<string, object> CachedObjects = new();
    private static readonly object _cacheObjectLock = new();

    public void Setup() { }

    public void Store<T>(T t)
        where T : IKeyable
    {
        lock (_cacheObjectLock)
        {
            CachedObjects.Add(GetLookupKey(t.GetKey()), t);
        }
    }

    public bool Exists(IKey t)
    {
        lock (_cacheObjectLock)
        {
            return CachedObjects.ContainsKey(GetLookupKey(t));
        }
    }

    public T? GetItem<T>(IKey key)
        where T : IKeyable
    {
        lock (_cacheObjectLock)
        {
            if (CachedObjects.TryGetValue(GetLookupKey(key), out var value))
            {
                return (T)value;
            }
        }

        return default;
    }

    public List<T> GetAll<T>()
    {
        throw new NotImplementedException();
    }

    private string GetLookupKey(IKey key)
    {
        return string.Join("-", key.GetLookupIndex());
    }

    public void Clear()
    {
        lock (_cacheObjectLock)
        {
            CachedObjects.Clear();
        }
    }
}
