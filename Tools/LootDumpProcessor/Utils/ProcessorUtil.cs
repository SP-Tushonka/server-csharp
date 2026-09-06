using System.Security.Cryptography;
using System.Text;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Utils;

public static class ProcessorUtil
{
    public static string GetSaneId(this SpawnpointTemplate x)
    {
        return $"({x.Position?.X}, {x.Position?.Y}, {x.Position?.Z}, {Math.Round(x.Rotation?.X ?? 0, 3)},"
            + $" {Math.Round(x.Rotation?.Y ?? 0, 3)}, {Math.Round(x.Rotation?.Z ?? 0, 3)},"
            + $" {x.UseGravity}, {x.IsGroupPosition})";
    }

    public static string GetLocationId(this SpawnpointTemplate x)
    {
        return $"({x.Position?.X}, {x.Position?.Y}, {x.Position?.Z})";
    }

    // Callers reassign Items on the copy, so the lists must not be shared with the source.
    public static SpawnpointTemplate DeepClone(this SpawnpointTemplate template)
    {
        return template with
        {
            GroupPositions = template.GroupPositions?.Select(g => g with { }).ToList(),
            Items = template.Items?.Select(DeepClone).ToList(),
        };
    }

    public static SptLootItem DeepClone(this SptLootItem item)
    {
        return item with { Upd = item.Upd == null ? null : item.Upd with { } };
    }

    public static string HashFile(string text)
    {
        var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
}

// Dumps of the same map repeat a spawn point under the same Id with fresh item instances.
public sealed class SpawnpointTemplateIdComparer : IEqualityComparer<SpawnpointTemplate>
{
    public static readonly SpawnpointTemplateIdComparer Instance = new();

    public bool Equals(SpawnpointTemplate x, SpawnpointTemplate y)
    {
        return x?.Id == y?.Id;
    }

    public int GetHashCode(SpawnpointTemplate obj)
    {
        return obj.Id?.GetHashCode() ?? 0;
    }
}
