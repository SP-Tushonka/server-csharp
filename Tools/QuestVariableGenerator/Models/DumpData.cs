using SPTarkov.Server.Core.Models.Common;

namespace QuestVariableGenerator.Models;

/// <summary>Observed quest-to-member associations and profile snapshots extracted from the input captures.</summary>
public record DumpData
{
    /// <summary>Maps each observed member to the sole quest completion in its items/moving request.</summary>
    public required Dictionary<MongoId, MongoId> LivePairs { get; init; }

    public required List<ProfileSnapshot> Snapshots { get; init; }
}
