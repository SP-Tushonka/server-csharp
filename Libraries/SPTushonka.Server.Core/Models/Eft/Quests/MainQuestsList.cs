using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record MainQuestsList
{
    [JsonPropertyName("chapters")]
    public required IEnumerable<MainQuestChapterId> Chapters { get; set; }
}

public record MainQuestChapterId
{
    [JsonPropertyName("ChapterId")]
    public MongoId ChapterId { get; set; }
}
