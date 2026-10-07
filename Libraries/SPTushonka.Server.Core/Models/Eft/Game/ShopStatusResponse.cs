using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ShopStatusResponse
{
    [JsonPropertyName("aid")]
    public int? Aid { get; set; }

    /// <summary>
    ///     Every label the shop menu carries, such as NEW or SALE, each set to 1
    /// </summary>
    [JsonPropertyName("labels")]
    public Dictionary<string, int>? Labels { get; set; }

    [JsonPropertyName("tarcoins")]
    public int? Tarcoins { get; set; }
}

public sealed record GameTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
