using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record TraderDialogs
{
    [JsonPropertyName("elements")]
    public required List<TraderDialogElement> Elements { get; init; }
}

public record TraderDialogElement
{
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

    [JsonPropertyName("Lines")]
    public required List<TraderDialogLine> Lines { get; set; }

    [JsonPropertyName("StartPoints")]
    public required Dictionary<MongoId, int> StartPoints { get; set; }

    [JsonPropertyName("localization")]
    [JsonConverter(typeof(EmptyArrayAsDictionaryConverter<string, Dictionary<MongoId, string>>))]
    public required Dictionary<string, Dictionary<MongoId, string>> LocalizationDictionary { get; set; }
}

public record TraderDialogLine
{
    [JsonPropertyName("Id")]
    public required MongoId Id { get; set; }

    [JsonPropertyName("DialogSide")]
    public required string DialogSide { get; set; }

    [JsonPropertyName("IconType")]
    public required string IconType { get; set; }

    [JsonPropertyName("ConfirmationKey")]
    public string? ConfirmationKey { get; set; }

    [JsonPropertyName("AnimationData")]
    public required TraderDialogAnimationData AnimationData { get; set; }

    [JsonPropertyName("Actions")]
    public required List<TraderDialogAction> Actions { get; set; }

    [JsonPropertyName("Trigger")]
    public required TraderDialogTrigger Trigger { get; set; }

    [JsonPropertyName("TraderId")]
    public MongoId? TraderId { get; set; }
}

public record TraderDialogAction
{
    [JsonPropertyName("id")]
    public required MongoId Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("needNotification")]
    public bool? NeedNotification { get; set; }

    [JsonPropertyName("questId")]
    public MongoId? QuestId { get; set; }

    [JsonPropertyName("conditionId")]
    public MongoId? ConditionId { get; set; }

    [JsonPropertyName("partialHandover")]
    public bool? PartialHandover { get; set; }

    [JsonPropertyName("dialogId")]
    public MongoId? DialogId { get; set; }

    [JsonPropertyName("splitterNodeId")]
    public MongoId? SplitterNodeId { get; set; }

    [JsonPropertyName("variableId")]
    public MongoId? VariableId { get; set; }

    // A number for SetVariable, text for DiaryNote
    [JsonPropertyName("value")]
    public StringOrInt? Value { get; set; }

    [JsonPropertyName("saveScope")]
    public string? SaveScope { get; set; }

    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; set; }

    [JsonPropertyName("subServiceId")]
    public string? SubServiceId { get; set; }
}

public record TraderDialogAnimationData
{
    [JsonPropertyName("animations")]
    public required List<TraderDialogAnimation> Animations { get; set; }

    [JsonPropertyName("secondaryAnimations")]
    public required List<TraderDialogAnimation> SecondaryAnimations { get; set; }

    [JsonPropertyName("lipSyncs")]
    public required List<TraderDialogLipSync> LipSyncs { get; set; }

    [JsonPropertyName("subtitles")]
    public required List<TraderDialogSubtitle> Subtitles { get; set; }

    [JsonPropertyName("media")]
    public required TraderDialogMedia Media { get; set; }
}

public record TraderDialogAnimation
{
    [JsonPropertyName("animationId")]
    public required string AnimationId { get; set; }

    [JsonPropertyName("start")]
    public required double Start { get; set; }

    [JsonPropertyName("end")]
    public required double End { get; set; }

    [JsonPropertyName("speed")]
    public required double Speed { get; set; }
}

public record TraderDialogLipSync
{
    [JsonPropertyName("lipSyncId")]
    public required string LipSyncId { get; set; }

    [JsonPropertyName("start")]
    public required double Start { get; set; }

    [JsonPropertyName("end")]
    public required double End { get; set; }

    [JsonPropertyName("volume")]
    public required double Volume { get; set; }
}

public record TraderDialogSubtitle
{
    [JsonPropertyName("id")]
    public required MongoId Id { get; set; }

    [JsonPropertyName("start")]
    public required double Start { get; set; }

    [JsonPropertyName("end")]
    public required double End { get; set; }
}

public record TraderDialogMedia
{
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("music")]
    public string? Music { get; set; }

    [JsonPropertyName("sound")]
    public string? Sound { get; set; }
}
