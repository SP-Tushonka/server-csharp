using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace QuestVariableGenerator.Models;

/// <summary>Generated quest mappings and unresolved candidate classes for one variable group.</summary>
public record QuestVariableGroup
{
    [JsonPropertyName("id")]
    public required MongoId Id { get; init; }

    /// <summary>Maps quests to individual members using observed associations or the solver's ordering heuristic.</summary>
    [JsonPropertyName("quests")]
    public Dictionary<MongoId, MongoId> Quests { get; } = [];

    /// <summary>Unresolved member counts and their candidate quests.</summary>
    [JsonPropertyName("fillers")]
    public List<FillerClass> Fillers { get; } = [];

    /// <summary>Unassigned quests gated on this group whose completion patterns match none of its members.</summary>
    [JsonPropertyName("notMembers")]
    public List<MongoId> NotMembers { get; } = [];
}
