using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Ws;

public record WsExpansionsLabelsUpdated : WsNotificationEvent
{
    [JsonPropertyName("newOffersCount")]
    public int NewOffersCount { get; set; }

    [JsonPropertyName("freeOffersCount")]
    public int FreeOffersCount { get; set; }
}
