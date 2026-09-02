using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Game;

namespace SPTarkov.Server.Core.Models.Spt.Tables;

public record ShopTable
{
    [JsonPropertyName("content")]
    public required ShopContent Content { get; init; }
}
