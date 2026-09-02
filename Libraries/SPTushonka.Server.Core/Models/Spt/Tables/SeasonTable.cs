using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Eft.Seasons;

namespace SPTarkov.Server.Core.Models.Spt.Tables;

public record SeasonTable
{
    [JsonPropertyName("active")]
    public required SeasonActiveResponse Active { get; init; }

    [JsonPropertyName("battlePass")]
    public required BattlePassActiveResponse BattlePass { get; init; }

    [JsonPropertyName("perks")]
    public required SeasonalPerksResponse Perks { get; init; }
}
