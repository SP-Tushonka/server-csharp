using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public sealed class MainQuestNotes
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("links")]
    public QuestChapterLinks Links { get; set; } = new();

    [JsonPropertyName("chapterId")]
    public string ChapterId { get; set; } = string.Empty;

    [JsonPropertyName("conditionIds")]
    public List<string> ConditionIds { get; set; } = [];
}

public sealed class QuestChapterLinks
{
    [JsonPropertyName("items")]
    public List<QuestItemLink> Items { get; set; } = [];

    [JsonPropertyName("offers")]
    public List<QuestOfferLink> Offers { get; set; } = [];

    [JsonPropertyName("crafts")]
    public List<QuestCraftLink> Crafts { get; set; } = [];
}

public sealed class QuestItemLink
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("quests")]
    public List<string> Quests { get; set; } = [];
}

public sealed class QuestOfferLink
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = string.Empty;

    [JsonPropertyName("item")]
    public List<QuestLinkedItem> Item { get; set; } = [];

    [JsonPropertyName("quests")]
    public List<string> Quests { get; set; } = [];
}

public sealed class QuestCraftLink
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public long Area { get; set; }

    [JsonPropertyName("item")]
    public List<QuestLinkedItem> Item { get; set; } = [];

    [JsonPropertyName("quests")]
    public List<string> Quests { get; set; } = [];
}

public sealed class QuestLinkedItem
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("_tpl")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("upd")]
    public QuestLinkedItemUpd? Upd { get; set; }
}

public sealed class QuestLinkedItemUpd
{
    [JsonPropertyName("StackObjectsCount")]
    public long StackObjectsCount { get; set; }
}
