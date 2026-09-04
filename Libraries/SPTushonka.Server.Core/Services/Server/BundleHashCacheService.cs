using System.Collections.Concurrent;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Bundles;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Services.Server;

[Injectable(InjectionType.Singleton)]
public sealed class BundleHashCacheService(JsonUtil jsonUtil, HashUtil hashUtil, FileUtil fileUtil)
{
    private const string BundleHashCachePath = "./user/cache/";
    private const string CacheName = "bundleHashCache.json";

    private ConcurrentDictionary<string, BundleHashCacheEntry> _loaded = [];
    private readonly ConcurrentDictionary<string, BundleHashCacheEntry> _current = [];
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task HydrateCacheAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(BundleHashCachePath);

        var fullCachePath = Path.Join(BundleHashCachePath, CacheName);

        if (!File.Exists(fullCachePath))
        {
            return;
        }

        try
        {
            _loaded =
                await jsonUtil.DeserializeFromFileAsync<ConcurrentDictionary<string, BundleHashCacheEntry>>(
                    fullCachePath,
                    cancellationToken
                ) ?? [];
        }
        catch (JsonException)
        {
            _loaded = [];
        }
    }

    /// <summary>
    ///     Return the bundle's CRC, hashing the file only when size or write time no longer match the cached entry.
    /// </summary>
    /// <param name="bundlePath">The path to the bundle</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that can be used to cancel the hashing operation.
    /// </param>
    /// <returns>The cache entry, or null when the file is not a Unity asset bundle</returns>
    public async Task<BundleHashCacheEntry?> GetOrCalculateHashAsync(string bundlePath, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(bundlePath);
        var size = fileInfo.Length;
        var modified = fileInfo.LastWriteTimeUtc.Ticks;

        if (_loaded.TryGetValue(bundlePath, out var cached) && cached.Size == size && cached.ModifiedUtcTicks == modified)
        {
            _current[bundlePath] = cached;
            return cached;
        }

        await using var fileStream = File.OpenRead(bundlePath);

        if (!await fileUtil.VerifyBundleHeaderAsync(fileStream, cancellationToken))
        {
            return null;
        }

        // Reset stream position so the whole file is calculated
        fileStream.Position = 0;

        var entry = new BundleHashCacheEntry
        {
            Size = size,
            ModifiedUtcTicks = modified,
            Crc = await hashUtil.GenerateCrc32ForFileAsync(fileStream, cancellationToken),
        };

        _current[bundlePath] = entry;

        return entry;
    }

    /// <summary>
    ///     Writes only the bundles seen this run, dropping entries belonging to removed mods.
    /// </summary>
    public async Task WriteCacheAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var serialized = jsonUtil.Serialize(_current);

            if (serialized is null)
            {
                return;
            }

            await fileUtil.WriteFileAsync(Path.Join(BundleHashCachePath, CacheName), serialized, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
