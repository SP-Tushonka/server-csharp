using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record SeasonalPerkEffectParameters
{
    [JsonPropertyName("allergy")]
    public Dictionary<MongoId, AllergyPerkEffectParameters>? Allergy { get; set; }
}

public record AllergyPerkEffectParameters
{
    [JsonPropertyName("targetItems")]
    public List<MongoId>? TargetItems { get; set; }
}
