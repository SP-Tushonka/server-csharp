using SPTarkov.Server.Core.Models.Common;

namespace QuestVariableGenerator.Models;

/// <summary>A quest's variable gate and ordering timestamp, or a synthetic entry with an empty target for an unconditional quest.</summary>
public record GatedQuest
{
    public required MongoId Quest { get; init; }

    public required MongoId Target { get; init; }

    /// <summary>Unix seconds from the condition ID, or the quest ID for an unconditional entry candidate.</summary>
    public required long ConditionCreated { get; init; }
}
