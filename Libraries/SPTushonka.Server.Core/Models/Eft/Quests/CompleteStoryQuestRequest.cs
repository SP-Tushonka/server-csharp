using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record CompleteStoryQuestRequest : IRequestData
{
    [JsonPropertyName("questId")]
    public MongoId QuestId { get; set; }
}
