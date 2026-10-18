using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Dialog;

namespace SPTarkov.Server.Core.Models.Spt.Config;

public record GiftsConfig : BaseConfig
{
    [JsonPropertyName("kind")]
    public override string Kind { get; set; } = "spt-gifts";

    [JsonPropertyName("gifts")]
    public required Dictionary<string, Gift> Gifts { get; set; }

    /// <summary>
    ///     Gear crates a trader mails a newly created or prestiged character
    /// </summary>
    [JsonPropertyName("initialSupport")]
    public InitialSupportSettings? InitialSupport { get; set; }
}

public record InitialSupportSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("trader")]
    public MongoId Trader { get; set; }

    /// <summary>
    ///     Days after the character was created during which crates are sent
    /// </summary>
    [JsonPropertyName("days")]
    public int Days { get; set; }

    /// <summary>
    ///     Hours between two crates
    /// </summary>
    [JsonPropertyName("intervalHours")]
    public int IntervalHours { get; set; }

    /// <summary>
    ///     Locale id of the message sent with the first crate
    /// </summary>
    [JsonPropertyName("firstMessageId")]
    public required string FirstMessageId { get; set; }

    /// <summary>
    ///     Locale id of the message sent with every later crate
    /// </summary>
    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }

    [JsonPropertyName("collectionTimeHours")]
    public int CollectionTimeHours { get; set; }

    /// <summary>
    ///     Crates to pick from, each needs contents in the inventory config
    /// </summary>
    [JsonPropertyName("containers")]
    public required List<MongoId> Containers { get; set; }
}

public record Gift
{
    /// <summary>
    ///     Items to send to player
    /// </summary>
    [JsonPropertyName("items")]
    public List<Item> Items { get; set; } = [];

    /// <summary>
    ///     Who is sending the gift to player
    /// </summary>
    [JsonPropertyName("sender")]
    public GiftSenderType Sender { get; set; }

    [JsonPropertyName("senderDetails")]
    public UserDialogInfo? SenderDetails { get; set; }

    /// <summary>
    ///     Optional - supply a trader type to send from, not necessary when sending from SYSTEM or USER
    /// </summary>
    [JsonPropertyName("trader")]
    public MongoId? Trader { get; set; }

    [JsonPropertyName("messageText")]
    public string? MessageText { get; set; }

    /// <summary>
    ///     Optional - if sending text from the client locale file
    /// </summary>
    [JsonPropertyName("localeTextId")]
    public string? LocaleTextId { get; set; }

    [JsonPropertyName("associatedEvent")]
    public SeasonalEventType AssociatedEvent { get; set; }

    [JsonPropertyName("collectionTimeHours")]
    public int? CollectionTimeHours { get; set; }

    /// <summary>
    ///     Optional, can be used to change profile settings like level/skills
    /// </summary>
    [JsonPropertyName("profileChangeEvents")]
    public List<ProfileChangeEvent>? ProfileChangeEvents { get; set; }

    [JsonPropertyName("maxToSendPlayer")]
    public int? MaxToSendPlayer { get; set; }

    /// <summary>Optional, tarcoins credited to the account's shop wallet when the gift is sent</summary>
    [JsonPropertyName("tarcoins")]
    public int? Tarcoins { get; set; }
}
