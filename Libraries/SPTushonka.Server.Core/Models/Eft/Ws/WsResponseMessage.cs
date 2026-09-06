using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Ws;

public record WsResponseMessage
{
    [JsonPropertyName("ID")]
    public string? Id { get; set; }

    [JsonPropertyName("Method")]
    public string? Method { get; set; }

    [JsonPropertyName("Status")]
    public string Status { get; set; } = "OK";

    [JsonPropertyName("Result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("Error")]
    public WsResponseError? Error { get; set; }
}

public record WsResponseError
{
    [JsonPropertyName("Code")]
    public string? Code { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }
}
