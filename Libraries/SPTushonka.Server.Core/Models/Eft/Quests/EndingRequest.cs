using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record EndingRequest : IRequestData
{
    [JsonPropertyName("endingId")]
    public MongoId EndingId { get; set; }
}

public record EndingLocalizationResponse
{
    [JsonPropertyName("Localization")]
    public required Dictionary<string, Dictionary<string, string>> Localization { get; set; }
}
