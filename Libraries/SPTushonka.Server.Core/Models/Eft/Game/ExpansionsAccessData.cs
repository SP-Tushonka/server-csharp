using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ExpansionsAccessData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
