using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums.Hideout;

namespace SPTarkov.Server.Core.Models.Eft.Hideout;

public record HideoutProductionData
{
    [JsonPropertyName("recipes")]
    public required List<HideoutProduction> Recipes { get; set; }

    [JsonPropertyName("scavRecipes")]
    public required List<ScavRecipe> ScavRecipes { get; set; }

    [JsonPropertyName("cultistRecipes")]
    public required List<CultistRecipe> CultistRecipes { get; set; }
}

public record HideoutProduction
{
    [JsonPropertyName("_id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("areaType")]
    public required HideoutAreas AreaType { get; set; }

    [JsonPropertyName("requirements")]
    public required List<Requirement> Requirements { get; set; }

    [JsonPropertyName("productionTime")]
    public required double ProductionTime { get; set; }

    /// <summary>
    ///     Tpl of item being crafted
    /// </summary>
    [JsonPropertyName("endProduct")]
    public MongoId EndProduct { get; set; }

    [JsonPropertyName("isEncoded")]
    public required bool IsEncoded { get; set; }

    [JsonPropertyName("locked")]
    public required bool Locked { get; set; }

    [JsonPropertyName("needFuelForAllProductionTime")]
    public required bool NeedFuelForAllProductionTime { get; set; }

    [JsonPropertyName("continuous")]
    public required bool Continuous { get; set; }

    [JsonPropertyName("count")]
    public required int Count { get; set; }

    [JsonPropertyName("productionLimitCount")]
    public required int ProductionLimitCount { get; set; }

    [JsonPropertyName("isCodeProduction")]
    public required bool IsCodeProduction { get; set; }
}

public record Requirement
{
    [JsonPropertyName("templateId")]
    public MongoId? TemplateId { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("isEncoded")]
    public bool? IsEncoded { get; set; }

    [JsonPropertyName("isFunctional")]
    public bool? IsFunctional { get; set; }

    [JsonPropertyName("areaType")]
    public int? AreaType { get; set; }

    [JsonPropertyName("requiredLevel")]
    public int? RequiredLevel { get; set; }

    [JsonPropertyName("resource")]
    public int? Resource { get; set; }

    [JsonPropertyName("questId")]
    public MongoId? QuestId { get; set; }

    [JsonPropertyName("isSpawnedInSession")]
    public bool? IsSpawnedInSession { get; set; }

    [JsonPropertyName("gameVersions")]
    public List<string>? GameVersions { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public record ScavRecipe
{
    [JsonPropertyName("_id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("requirements")]
    public required List<Requirement> Requirements { get; set; }

    [JsonPropertyName("productionTime")]
    public required double ProductionTime { get; set; }

    [JsonPropertyName("endProducts")]
    public required EndProducts EndProducts { get; set; }
}

public record EndProducts
{
    [JsonPropertyName("Common")]
    public required MinMax<int> Common { get; set; }

    [JsonPropertyName("Rare")]
    public required MinMax<int> Rare { get; set; }

    [JsonPropertyName("Superrare")]
    public required MinMax<int> Superrare { get; set; }
}

public record CultistRecipe
{
    [JsonPropertyName("_id")]
    public MongoId Id { get; set; }
}
