using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Ws;

public record WsExpansionsOffer : WsNotificationEvent
{
    [JsonPropertyName("offerId")]
    public string? OfferId { get; set; }

    // The client picks the popup icon from these, sent as EBonusType names
    [JsonPropertyName("bonusTypes")]
    public List<string> BonusTypes { get; set; } = [];
}
