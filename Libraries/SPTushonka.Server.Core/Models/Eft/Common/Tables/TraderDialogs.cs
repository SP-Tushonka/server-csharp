using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record TraderDialogs
{
    [JsonPropertyName("elements")]
    public required List<TraderDialogElement> Elements { get; init; }
}

public record TraderDialogElement
{
    [JsonPropertyName("CanBeFirstDialogue")]
    public bool CanBeFirstDialog { get; set; } = true;

    [JsonPropertyName("Id")]
    public required MongoId Id { get; set; }

    [JsonPropertyName("IsStart")]
    public required bool IsStart { get; set; }

    [JsonPropertyName("MainVariable")]
    public required MongoId MainVariable { get; set; }

    [JsonPropertyName("Trader")]
    public required MongoId MainTrader { get; set; }

    [JsonPropertyName("SubTraders")]
    public required List<MongoId> SubTraders { get; set; }

    //Todo: Still needs fixing up
    [JsonPropertyName("Lines")]
    public required List<object> Lines { get; set; }

    [JsonPropertyName("StartPoints")]
    public required Dictionary<MongoId, int> StartPoints { get; set; }

    [JsonPropertyName("localization")]
    [JsonConverter(typeof(EmptyArrayAsDictionaryConverter<string, Dictionary<MongoId, string>>))]
    public required Dictionary<string, Dictionary<MongoId, string>> LocalizationDictionary { get; set; }
}
