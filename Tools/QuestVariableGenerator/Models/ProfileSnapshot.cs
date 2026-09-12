using SPTarkov.Server.Core.Models.Common;

namespace QuestVariableGenerator.Models;

/// <summary>A captured PMC profile's nonzero variables and successfully completed quests.</summary>
public record ProfileSnapshot
{
    public required HashSet<MongoId> Variables { get; init; }

    public required HashSet<MongoId> Completed { get; init; }
}
