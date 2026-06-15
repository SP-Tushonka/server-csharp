using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record VariableGroupData
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("variables")]
    public required List<MongoId> Variables { get; set; }
}
