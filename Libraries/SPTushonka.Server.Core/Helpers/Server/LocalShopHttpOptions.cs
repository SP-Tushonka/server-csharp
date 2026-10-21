namespace SPTarkov.Server.Core.Helpers.Server;

/// <summary>Explicit loopback HTTP transport shared by the embedded shop and notification channel.</summary>
public static class LocalShopHttpOptions
{
    public const string EnableVariable = "SPT_LOCAL_SHOP_HTTP";
    public const int Port = 6970;
    public static bool Enabled => Environment.GetEnvironmentVariable(EnableVariable) switch
    {
        null or "" or "0" => false,
        "1" => true,
        _ => throw new InvalidOperationException("SPT_LOCAL_SHOP_HTTP must be 0 or 1."),
    };
    public static string NotifierBaseUrl => $"ws://127.0.0.1:{Port}";
}
