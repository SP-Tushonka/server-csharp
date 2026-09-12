using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Utils.Json.Converters;

/// <summary>
/// Picks the condition record by its type field.
/// </summary>
public sealed class TraderDialogConditionConverter : JsonConverter<TraderDialogCondition>
{
    public override TraderDialogCondition? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var jsonText = jsonDocument.RootElement.GetRawText();

        if (!jsonDocument.RootElement.TryGetProperty("type", out var typeElement))
        {
            throw new JsonException("Could not deserialize dialogue condition. Property 'type' is missing.");
        }

        var type = typeElement.GetString();

        return type switch
        {
            "VariableValue" => JsonSerializer.Deserialize<TraderDialogVariableValueCondition>(jsonText, options),
            "QuestStatus" => JsonSerializer.Deserialize<TraderDialogQuestStatusCondition>(jsonText, options),
            "QuestConditionStatus" => JsonSerializer.Deserialize<TraderDialogQuestConditionStatusCondition>(jsonText, options),
            "HasItemForHandover" => JsonSerializer.Deserialize<TraderDialogHasItemForHandoverCondition>(jsonText, options),
            "CurrentTrader" => JsonSerializer.Deserialize<TraderDialogCurrentTraderCondition>(jsonText, options),
            "TraderReputation" => JsonSerializer.Deserialize<TraderDialogTraderReputationCondition>(jsonText, options),
            "HasNewQuests" => JsonSerializer.Deserialize<TraderDialogHasNewQuestsCondition>(jsonText, options),
            "ServiceAvailable" => JsonSerializer.Deserialize<TraderDialogServiceAvailableCondition>(jsonText, options),
            "CompletableItem" => JsonSerializer.Deserialize<TraderDialogCompletableItemCondition>(jsonText, options),
            "HasFreeSpecialSlot" => JsonSerializer.Deserialize<TraderDialogHasFreeSpecialSlotCondition>(jsonText, options),
            _ => throw new JsonException($"Unhandled dialogue condition type '{type}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, TraderDialogCondition value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
