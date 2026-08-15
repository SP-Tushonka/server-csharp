namespace SPTarkov.Server.Core.Models.Spt.Bundles;

public sealed record BundleHashCacheEntry
{
    public required long Size { get; init; }

    public required long ModifiedUtcTicks { get; init; }

    public required uint Crc { get; init; }
}
