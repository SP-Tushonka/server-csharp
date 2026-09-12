using SPTarkov.Server.Core.Models.Common;

namespace QuestVariableGenerator.Models;

/// <summary>Candidate quests partitioned by priority, with the solver's pairing order preserved within each list.</summary>
public record CandidateQuests
{
    /// <summary>Entry candidates followed by quests gated on the group, ordered by creation timestamp within each segment.</summary>
    public required List<MongoId> Expected { get; init; }

    /// <summary>Remaining quests accepted by the trader filter, ordered by quest creation timestamp.</summary>
    public required List<MongoId> SameTrader { get; init; }

    /// <summary>All other quests, ordered by quest creation timestamp.</summary>
    public required List<MongoId> Others { get; init; }
}
