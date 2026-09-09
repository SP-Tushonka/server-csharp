using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Ws;

public record WsExpansionsBalance : WsNotificationEvent
{
    [JsonPropertyName("balance")]
    public int Balance { get; set; }
}
