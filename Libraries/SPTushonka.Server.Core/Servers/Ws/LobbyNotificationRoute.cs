using SPTarkov.Server.Core.Models.Eft.Ws;

namespace SPTarkov.Server.Core.Servers.Ws;

/// <summary>Lobby headers use EFT's ENotificationType names, not the legacy JSON type identifiers.</summary>
public static class LobbyNotificationRoute
{
    public static string? For(NotificationEventType? type) => type switch
    {
        NotificationEventType.new_message => "ChatMessageReceived",
        NotificationEventType.shopLabelsUpdated => "ExpansionsLabelsUpdated",
        NotificationEventType.UpdateAccountTarcoinBalance => "ExpansionsAccountTarcoinBalance",
        NotificationEventType.UpdateAccountOfferPurchased => "ExpansionsAccountOfferPurchased",
        NotificationEventType.UpdateAccountOfferReceived => "ExpansionsAccountOfferReceived",
        NotificationEventType.UpdateAccountTarcoinBalanceIncreased => "ExpansionsAccountBalanceIncreased",
        NotificationEventType.OffersCompositionChanged => "ExpansionsOffersCompositionChanged",
        NotificationEventType.SaleContentChanged => "ExpansionsSaleContentChanged",
        NotificationEventType.MenuLabelsChanged => "ExpansionsMenuLabelsChanged",
        NotificationEventType.GameEditionUpgrade => "ExpansionsGameEditionUpgrade",
        NotificationEventType.UpdateAccountEftBattlePassUniversalDocumentBalance => "BattlePassUniversalDocuments",
        _ => null,
    };
}
