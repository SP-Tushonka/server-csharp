using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ShopData<T>
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("err")]
    public object? Err { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("errmsg")]
    public object? ErrMsg { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("status")]
    public object? Status { get; set; }

    [JsonPropertyName("errLog")]
    public Dictionary<string, object> ErrLog { get; set; } = [];

    [JsonPropertyName("error")]
    public ShopEnvelopeError Error { get; set; } = new();
}

public sealed record ShopEnvelopeError
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("code")]
    public object? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("message")]
    public object? Message { get; set; }
}
