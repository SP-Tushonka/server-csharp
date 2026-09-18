using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Spt.Config;

public record HealthConfig : BaseConfig
{
    [JsonPropertyName("kind")]
    public override string Kind { get; set; } = "spt-health";

    [JsonPropertyName("healthMultipliers")]
    public required HealthMultipliers HealthMultipliers { get; set; }

    [JsonPropertyName("save")]
    public required HealthSave Save { get; set; }

    /// <summary>
    ///     How often the stored health of active profiles is advanced, in seconds
    /// </summary>
    [JsonPropertyName("runIntervalSeconds")]
    public int RunIntervalSeconds { get; set; }

    [JsonPropertyName("updateProfileHealthWhenActiveWithinMinutes")]
    public int UpdateProfileHealthWhenActiveWithinMinutes { get; set; }
}

public record HealthMultipliers
{
    [JsonPropertyName("blacked")]
    public double Blacked { get; set; }

    [JsonPropertyName("death")]
    public double Death { get; set; }
}

public record HealthSave
{
    [JsonPropertyName("health")]
    public bool Health { get; set; }
}
