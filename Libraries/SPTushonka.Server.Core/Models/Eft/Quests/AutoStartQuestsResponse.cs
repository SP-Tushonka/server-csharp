using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record AutoStartQuestsResponse
{
    [JsonPropertyName("templates")]
    public required List<Common.Tables.Quest> Templates { get; set; }

    [JsonPropertyName("deletedQuestItems")]
    public required List<MongoId> DeletedQuestItems { get; set; }
}
