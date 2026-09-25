using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Spt.Config;

public record BtrDeliveryConfig : BaseConfig
{
    [JsonPropertyName("kind")]
    public override string Kind { get; set; } = "spt-btrdelivery";

    /// <summary>
    /// Override to control how quickly delivery is processed/returned in seconds
    /// </summary>
    [JsonPropertyName("returnTimeOverrideSeconds")]
    public double ReturnTimeOverrideSeconds { get; set; }

    /// <summary>
    /// Min/max time in seconds before a BTR or transit delivery is sent to the player
    /// </summary>
    [JsonPropertyName("returnTimeSeconds")]
    public MinMax<double> ReturnTimeSeconds { get; set; } = new(3600, 3600);

    /// <summary>
    /// How often server should process BTR delivery in seconds
    /// </summary>
    [JsonPropertyName("runIntervalSeconds")]
    public double RunIntervalSeconds { get; set; }
}
