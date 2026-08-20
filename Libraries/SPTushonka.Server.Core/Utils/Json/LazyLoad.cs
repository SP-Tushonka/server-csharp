namespace SPTarkov.Server.Core.Utils.Json;

public class LazyLoad<T>(Func<T> deserialize, bool cacheValue = false)
{
    private readonly Lock _valueLock = new();
    private T? _cachedValue;
    private bool _hasCachedValue;

    [Obsolete("This constructor will be removed in a newer version of SPT, use the constructor accepting cacheValue")]
    public LazyLoad(Func<T> deserialize)
        : this(deserialize, false) { }

    private readonly List<Func<T?, T?>> _lazyLoadTransformers = [];
    private readonly ReaderWriterLockSlim _lazyLoadTransformersLock = new();

    /// <summary>
    /// Adds a transformer to modify the value during lazy loading. Transformers execute
    /// in registration order.
    /// </summary>
    /// <param name="transformer">Function that transforms the value</param>
    public void AddTransformer(Func<T?, T?> transformer)
    {
        _lazyLoadTransformersLock.EnterWriteLock();

        try
        {
            _lazyLoadTransformers.Add(transformer);
        }
        finally
        {
            _lazyLoadTransformersLock.ExitWriteLock();
        }

        // Anything already built used the old transformer list
        Clear();
    }

    /// <summary>
    /// Drop any cached value so the next read deserialises again
    /// </summary>
    public void Clear()
    {
        lock (_valueLock)
        {
            _cachedValue = default;
            _hasCachedValue = false;
        }
    }

    public T? Value
    {
        get
        {
            if (!cacheValue)
            {
                return Build();
            }

            lock (_valueLock)
            {
                if (!_hasCachedValue)
                {
                    _cachedValue = Build();
                    _hasCachedValue = true;
                }

                return _cachedValue;
            }
        }
    }

    private T? Build()
    {
        var result = deserialize();

        _lazyLoadTransformersLock.EnterReadLock();
        try
        {
            foreach (var transform in _lazyLoadTransformers)
            {
                result = transform(result);
            }
        }
        finally
        {
            _lazyLoadTransformersLock.ExitReadLock();
        }

        return result;
    }
}
