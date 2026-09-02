using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ShopStatusResponse
{
    [JsonPropertyName("aid")]
    public int? Aid { get; set; }

    [JsonPropertyName("labels")]
    public List<object>? Labels { get; set; }

    [JsonPropertyName("tarcoins")]
    public int? Tarcoins { get; set; }
}

public sealed record GameTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
