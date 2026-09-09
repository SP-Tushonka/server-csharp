using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Ws;

public record WsExpansionsGameEditionUpgrade : WsNotificationEvent
{
    // A locale key, the client localises it for the popup
    [JsonPropertyName("editionName")]
    public string? EditionName { get; set; }
}
