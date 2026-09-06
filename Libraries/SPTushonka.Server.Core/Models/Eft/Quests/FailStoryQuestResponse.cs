using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record FailStoryQuestResponse
{
    [JsonPropertyName("quests")]
    public required List<Quest> Quests { get; set; }

    [JsonPropertyName("questsStatus")]
    public required List<QuestStatus> QuestsStatus { get; set; }
}
