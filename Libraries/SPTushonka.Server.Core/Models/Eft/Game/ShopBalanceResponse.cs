using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public sealed record ShopBalanceResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("data")]
    public ShopBalanceData? Data { get; set; }

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
    public Dictionary<string, object> ErrLog { get; set; } = new();

    [JsonPropertyName("error")]
    public ShopBalanceError Error { get; set; } = new();
}

public sealed record ShopBalanceData
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("item")]
    public ShopBalanceItem? Item { get; set; }
}

public sealed record ShopBalanceItem
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("balance")]
    public int? Balance { get; set; }
}

public sealed record ShopBalanceError
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("code")]
    public object? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("message")]
    public object? Message { get; set; }
}
