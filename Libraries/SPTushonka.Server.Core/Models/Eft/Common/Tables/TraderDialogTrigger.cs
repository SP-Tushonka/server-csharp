using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils.Json;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

/// <summary>The conditions under which the client shows a dialogue line.</summary>
public record TraderDialogTrigger
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("Conditions")]
    public required List<TraderDialogConditionGroup> Conditions { get; set; }

    [JsonPropertyName("Random")]
    public TraderDialogTriggerRandom? Random { get; set; }
}

public record TraderDialogTriggerRandom
{
    [JsonPropertyName("GroupId")]
    public required int GroupId { get; set; }

    [JsonPropertyName("VariableName")]
    public required MongoId VariableName { get; set; }

    [JsonPropertyName("StartValue")]
    public required int StartValue { get; set; }

    [JsonPropertyName("EndValue")]
    public required int EndValue { get; set; }

    [JsonPropertyName("MaxValue")]
    public required int MaxValue { get; set; }
}

public record TraderDialogConditionGroup
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("Conditions")]
    public required List<TraderDialogCondition> Conditions { get; set; }
}

public abstract record TraderDialogCondition
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}

public record TraderDialogVariableValueCondition : TraderDialogCondition
{
    [JsonPropertyName("variableId")]
    public required MongoId VariableId { get; set; }

    [JsonPropertyName("value")]
    public required int Value { get; set; }

    [JsonPropertyName("operator")]
    public required string Operator { get; set; }
}

public record TraderDialogQuestStatusCondition : TraderDialogCondition
{
    [JsonPropertyName("QuestId")]
    public required MongoId QuestId { get; set; }

    // Live mixes status numbers and names in the same list
    [JsonPropertyName("Status")]
    public required List<StringOrInt> Status { get; set; }
}

public record TraderDialogQuestConditionStatusCondition : TraderDialogCondition
{
    [JsonPropertyName("QuestId")]
    public required MongoId QuestId { get; set; }

    [JsonPropertyName("ConditionId")]
    public required MongoId ConditionId { get; set; }

    [JsonPropertyName("Status")]
    public required bool Status { get; set; }
}

public record TraderDialogHasItemForHandoverCondition : TraderDialogCondition
{
    [JsonPropertyName("questId")]
    public required MongoId QuestId { get; set; }

    [JsonPropertyName("conditionId")]
    public required MongoId ConditionId { get; set; }

    [JsonPropertyName("partial")]
    public required bool Partial { get; set; }
}

public record TraderDialogCurrentTraderCondition : TraderDialogCondition
{
    [JsonPropertyName("traderId")]
    public required MongoId TraderId { get; set; }
}

public record TraderDialogTraderReputationCondition : TraderDialogCondition
{
    [JsonPropertyName("TraderId")]
    public required MongoId TraderId { get; set; }

    [JsonPropertyName("Operator")]
    public required string Operator { get; set; }

    [JsonPropertyName("ReputationValue")]
    public required double ReputationValue { get; set; }
}

public record TraderDialogHasNewQuestsCondition : TraderDialogCondition
{
    [JsonPropertyName("TraderId")]
    public required MongoId TraderId { get; set; }

    [JsonPropertyName("Value")]
    public required bool Value { get; set; }
}

public record TraderDialogServiceAvailableCondition : TraderDialogCondition
{
    [JsonPropertyName("serviceType")]
    public required string ServiceType { get; set; }
}

public record TraderDialogCompletableItemCondition : TraderDialogCondition
{
    [JsonPropertyName("completableItemId")]
    public required MongoId CompletableItemId { get; set; }

    [JsonPropertyName("isCompleted")]
    public required bool IsCompleted { get; set; }
}

public record TraderDialogHasFreeSpecialSlotCondition : TraderDialogCondition
{
    [JsonPropertyName("hasFreeSlot")]
    public required bool HasFreeSlot { get; set; }
}
