using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Ws;

public record WsExpansionsBalanceIncreased : WsNotificationEvent
{
    [JsonPropertyName("amount")]
    public int Amount { get; set; }
}
