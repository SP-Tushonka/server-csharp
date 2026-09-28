using System.Collections.Concurrent;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;

namespace SPTarkov.Server.Core.Services.Commerce;

[Injectable(InjectionType.Singleton)]
public class TarcoinStoreService(
    ISptLogger<TarcoinStoreService> logger,
    SaveServer saveServer,
    ShopTable shopTable,
    MailSendService mailSendService,
    ProfileHelper profileHelper,
    NotificationSendHelper notificationSendHelper,
    CoreConfig coreConfig
)
{
    public int GetBalance(MongoId sessionId)
    {
        return saveServer.GetProfile(sessionId)?.CharacterData?.PmcData?.TarCoinBalance ?? 0;
    }

    public async Task CreditAsync(MongoId sessionId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetBalance(sessionId, GetBalance(sessionId) + amount);
        await NotifyAsync(
            sessionId,
            new WsExpansionsBalanceIncreased { EventType = NotificationEventType.UpdateAccountTarcoinBalanceIncreased, Amount = amount }
        );
        await NotifyBalanceAsync(sessionId);
    }

    // False when the balance is short, which leaves it untouched.
    public bool TrySpend(MongoId sessionId, int cost)
    {
        var balance = GetBalance(sessionId);
        if (cost <= 0 || balance < cost)
        {
            logger.Debug($"TarCoin spend of {cost} refused for {sessionId}, balance is {balance}");

            return false;
        }

        SetBalance(sessionId, balance - cost);

        return true;
    }

    public ShopBalanceResponse GetBalanceResponse(MongoId sessionId)
    {
        return new ShopBalanceResponse { Data = new ShopBalanceData { Item = new ShopBalanceItem { Balance = GetBalance(sessionId) } } };
    }

    /// <summary>
    ///     Get the Shop tabs to show. A tab is hidden once none of its tiles is shown, and a grouping tab once none of its children is
    /// </summary>
    /// <returns>Tabs in menu order</returns>
    public List<ShopMenuItem> GetMenu()
    {
        var menu = shopTable.Content.Menu;

        bool Shown(ShopMenuItem item)
        {
            if (item.PageId is not null)
            {
                return GetPage(item.PageId.Value.ToString())?.Blocks.Count > 0;
            }

            return menu.Any(child => child.ParentId == item.Id.ToString() && Shown(child));
        }

        return menu.Where(item =>
                Shown(item) && (item.ParentId is null || menu.Any(parent => parent.Id.ToString() == item.ParentId && Shown(parent)))
            )
            .OrderBy(item => item.Order)
            .ToList();
    }

    /// <summary>
    ///     Get a page with only the tiles this server shows, rows left empty by the rest closed up
    /// </summary>
    /// <param name="pageId">Page to get</param>
    /// <returns>A copy of the page, or null when there is no such page</returns>
    public ShopPage? GetPage(string pageId)
    {
        var page = shopTable.Content.Pages.FirstOrDefault(page => page.Id.ToString() == pageId);
        if (page is null)
        {
            return null;
        }

        var blocks = page.Blocks.Where(IsShown).ToList();
        if (blocks.Count == page.Blocks.Count)
        {
            return page;
        }

        var usedRows = blocks.SelectMany(block => Enumerable.Range(block.Position.Y, RowSpan(block))).ToHashSet();

        return page with
        {
            Blocks = blocks
                .Select(block => block with
                {
                    Position = new ShopGridPosition { X = block.Position.X, Y = block.Position.Y - Enumerable.Range(0, block.Position.Y).Count(row => !usedRows.Contains(row)) },
                })
                .ToList(),
        };
    }

    /// <summary>
    ///     Check whether a tile shows. Real money offers go through Xsolla or Steam, so they only show when
    ///     the server is set to show them and can never be bought here. An offer that delivers nothing this
    ///     server can grant never shows
    /// </summary>
    /// <param name="block">Tile to check</param>
    /// <returns>True when the tile shows</returns>
    private bool IsShown(ShopBlock block)
    {
        if (block.PurchaseMethod != "INTERNAL_CURRENCY")
        {
            return coreConfig.Features.ShowRealMoneyShopOffers;
        }

        var offer = GetOffer(block.OfferId.ToString());

        return offer is not null && IsDeliverable(offer);
    }

    /// <summary>
    ///     Get how many grid rows a tile spans. The aspect ratio is rows first, so "1:4" is one row four columns wide
    /// </summary>
    /// <param name="block">Tile to measure</param>
    /// <returns>Rows spanned, at least one</returns>
    private static int RowSpan(ShopBlock block)
    {
        var rows = block.AspectRatio.Split(':')[0];

        return int.TryParse(rows, out var span) && span > 0 ? span : 1;
    }

    private static readonly HashSet<string> DeliverableTypes =
    [
        ShopOfferItemType.Customization,
        ShopOfferItemType.StashRows,
        ShopOfferItemType.BattlePassUniversalDocument,
    ];

    /// <summary>
    ///     Check every part of an offer resolves to something this server can grant
    /// </summary>
    /// <param name="offer">Offer to check</param>
    /// <returns>False for a real money offer, a PvE upgrade or a bundle with an unknown part</returns>
    public bool IsDeliverable(ShopOffer offer)
    {
        if (offer.PurchaseMethod != "INTERNAL_CURRENCY")
        {
            return false;
        }

        var deliverables = CollectDeliverables(offer, out _, out var missing);

        return missing.Count == 0
            && deliverables.Count > 0
            && deliverables.All(entry => entry.Template is not null || DeliverableTypes.Contains(entry.Type ?? string.Empty));
    }

    public ShopOffer? GetOffer(string offerId)
    {
        return shopTable.Content.Offers.FirstOrDefault(offer => offer.Id.ToString() == offerId);
    }

    public List<ShopPrice> GetPrices(IEnumerable<string> offerIds)
    {
        var wanted = offerIds.ToHashSet();

        return shopTable.Content.Prices.Where(price => wanted.Contains(price.Id.ToString())).ToList();
    }

    /// <summary>
    ///     Get a Shop string. A language missing a string falls back to english, and a key neither has
    ///     reads as empty so the page leaves it out
    /// </summary>
    /// <param name="key">Locale key</param>
    /// <param name="language">Language code</param>
    /// <returns>The string, or empty</returns>
    public string Localise(string? key, string language = "en")
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (
            shopTable.Content.Locale.TryGetValue(language, out var strings)
            && strings.TryGetValue(key, out var value)
            && !string.IsNullOrEmpty(value)
        )
        {
            return value;
        }

        return shopTable.Content.Locale.TryGetValue("en", out var english) && english.TryGetValue(key, out var fallback)
            ? fallback
            : string.Empty;
    }

    public async Task<ShopPurchaseReceipt> TryPurchaseAsync(MongoId sessionId, string offerId, int count, CancellationToken cancellationToken = default)
    {
        var offer = GetOffer(offerId);
        var price = shopTable.Content.Prices.FirstOrDefault(entry => entry.Id.ToString() == offerId);

        if (offer is null || price is null)
        {
            logger.Warning($"TarCoin purchase refused, unknown or unpriced offer {offerId}");

            return new ShopPurchaseReceipt(ShopPurchaseOutcome.OfferNotFound);
        }

        var quantity = count < 1 ? 1 : count;

        if (!offer.Countable && HasPurchased(sessionId, offer))
        {
            logger.Warning($"TarCoin purchase refused, offer {offerId} has already been bought");

            return new ShopPurchaseReceipt(ShopPurchaseOutcome.PurchaseLimitExceeded);
        }

        var deliverables = CollectDeliverables(offer, out var boughtOfferIds, out var missingOfferIds);
        if (missingOfferIds.Count > 0)
        {
            logger.Warning($"TarCoin purchase refused, offer {offerId} contains unknown offers {string.Join(", ", missingOfferIds)}");

            return new ShopPurchaseReceipt(ShopPurchaseOutcome.NotDeliverable);
        }

        if (!IsDeliverable(offer))
        {
            logger.Warning($"TarCoin purchase refused, offer {offerId} has nothing this server can deliver");

            return new ShopPurchaseReceipt(ShopPurchaseOutcome.NotDeliverable);
        }

        if (!TrySpend(sessionId, price.AmountWithDiscount * quantity))
        {
            return new ShopPurchaseReceipt(ShopPurchaseOutcome.InsufficientBalance);
        }

        var items = new List<Item>();
        var bonusTypes = new List<BonusType>();
        var notifications = new List<Func<Task>>();
        var documentsChanged = false;
        var profile = saveServer.GetProfile(sessionId);

        foreach (var entry in deliverables)
        {
            if (entry.Template is not null)
            {
                items.Add(
                    new Item
                    {
                        Id = new MongoId(),
                        Template = entry.Template.Value,
                        Upd = new Upd { StackObjectsCount = entry.Count * quantity },
                    }
                );
                bonusTypes.Add(BonusType.ReceiveItemBonus);

                continue;
            }

            if (profile is null)
            {
                continue;
            }

            if (entry.Type == ShopOfferItemType.BattlePassUniversalDocument)
            {
                var pmc = profile.CharacterData?.PmcData;
                if (pmc is not null)
                {
                    pmc.BattlePassUniversalDocumentBalance = (pmc.BattlePassUniversalDocumentBalance ?? 0) + entry.Quantity * quantity;
                    documentsChanged = true;
                }

                continue;
            }

            if (entry.Type == ShopOfferItemType.StashRows)
            {
                var rows = entry.RowsCount * quantity;
                profileHelper.AddStashRowsBonusToProfile(sessionId, rows);
                profile.PurchasedShopStashRows = (profile.PurchasedShopStashRows ?? 0) + rows;
                bonusTypes.Add(BonusType.StashRows);

                // The client adds the rows to the profile the change is keyed by
                var stashRowsChange = new WsProfileChangeEvent
                {
                    EventType = NotificationEventType.StashRows,
                    Changes = new Dictionary<string, double?> { { profile.CharacterData!.PmcData!.Id.ToString(), rows } },
                };
                notifications.Add(() => NotifyAsync(sessionId, stashRowsChange));

                continue;
            }

            if (entry.CustomizationId is null)
            {
                logger.Warning($"Offer {offerId} carries a {entry.Type} entry that cannot be delivered");

                continue;
            }

            profile.CustomisationUnlocks ??= [];
            bonusTypes.Add(BonusType.Customization);
            if (ApplyHideoutCustomisation(profile.CharacterData?.PmcData, entry))
            {
                // The client only reinstalls a hideout look from a reloaded profile, and StashRows is a bonus it reloads for
                bonusTypes.Add(BonusType.StashRows);
            }

            if (profile.CustomisationUnlocks.Exists(unlock => Equals(unlock.Id, entry.CustomizationId.Value)))
            {
                continue;
            }

            profile.CustomisationUnlocks.Add(
                new CustomisationStorage
                {
                    Id = entry.CustomizationId.Value,
                    Source = CustomisationSource.UNLOCKED_IN_GAME,
                    Type = entry.CustomizationType ?? CustomisationType.SUITE,
                }
            );
        }

        if (items.Count > 0)
        {
            mailSendService.SendSystemMessageToPlayer(sessionId, $"Purchased: {Localise(offer.NameKey)}", items);
        }

        RecordPurchase(sessionId, boughtOfferIds);

        // The client shows the purchase popup and refreshes its balance from these, it never re-reads the shop status
        var purchased = new WsExpansionsOffer
        {
            EventType = NotificationEventType.UpdateAccountOfferPurchased,
            OfferId = offerId,
            BonusTypes = bonusTypes.Distinct().Select(bonus => bonus.ToString()).ToList(),
        };
        notifications.Add(() => NotifyAsync(sessionId, purchased));
        notifications.Add(() => NotifyBalanceAsync(sessionId));
        if (documentsChanged)
        {
            var documents = new WsExpansionsBalance
            {
                EventType = NotificationEventType.UpdateAccountEftBattlePassUniversalDocumentBalance,
                Balance = profile?.CharacterData?.PmcData?.BattlePassUniversalDocumentBalance ?? 0,
            };
            notifications.Add(() => NotifyAsync(sessionId, documents));
        }

        await saveServer.SaveProfileAsync(sessionId, cancellationToken);

        var transactionId = new MongoId().ToString();
        _unsignedPurchases[transactionId] = new UnsignedPurchase(sessionId, notifications);
        _ = SignLaterAsync(sessionId, transactionId);

        return new ShopPurchaseReceipt(ShopPurchaseOutcome.Success, transactionId);
    }

    /// <summary>
    ///     Purchases waiting on the game's signature. The shop page asks the game to sign two seconds after
    ///     buying, and the notifications are held until it does
    /// </summary>
    private readonly ConcurrentDictionary<string, UnsignedPurchase> _unsignedPurchases = new();

    private sealed record UnsignedPurchase(MongoId SessionId, List<Func<Task>> Notifications);

    /// <summary>
    ///     Send the notifications a purchase held back until the game signed its transaction
    /// </summary>
    /// <param name="sessionId">Session signing the transaction</param>
    /// <param name="transactionId">Transaction from the purchase</param>
    /// <returns>False when there is no such unsigned transaction for the session</returns>
    public async Task<bool> SignPurchaseAsync(MongoId sessionId, string? transactionId)
    {
        if (
            transactionId is null
            || !_unsignedPurchases.TryGetValue(transactionId, out var purchase)
            || !Equals(purchase.SessionId, sessionId)
            || !_unsignedPurchases.TryRemove(transactionId, out _)
        )
        {
            return false;
        }

        foreach (var notify in purchase.Notifications)
        {
            await notify();
        }

        return true;
    }

    /// <summary>
    ///     Sign a purchase the game never signed. A page opened outside the game has no bridge to ask for the signature
    /// </summary>
    /// <param name="sessionId">Session that bought</param>
    /// <param name="transactionId">Transaction from the purchase</param>
    private async Task SignLaterAsync(MongoId sessionId, string transactionId)
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        await SignPurchaseAsync(sessionId, transactionId);
    }

    /// <summary>
    ///     Keys of the profile's hideout customisation by customisation type, as HideoutCustomizationApply writes them
    /// </summary>
    private static readonly Dictionary<string, string> HideoutCustomisationSlots = new()
    {
        { CustomisationType.WALL, "Wall" },
        { CustomisationType.FLOOR, "Floor" },
        { CustomisationType.CEILING, "Ceiling" },
        { CustomisationType.LIGHT, "Light" },
        { CustomisationType.SHOOTING_RANGE_MARK, "ShootingRangeMark" },
    };

    /// <summary>
    ///     Make a bought hideout look the active one
    /// </summary>
    /// <param name="pmc">Buyer's PMC</param>
    /// <param name="entry">Customisation being delivered</param>
    /// <returns>True when the hideout look changed</returns>
    public static bool ApplyHideoutCustomisation(PmcData? pmc, ShopOfferItem entry)
    {
        if (
            pmc?.Hideout is null
            || entry.CustomizationId is null
            || entry.CustomizationType is null
            || !HideoutCustomisationSlots.TryGetValue(entry.CustomizationType, out var slot)
        )
        {
            return false;
        }

        pmc.Hideout.Customization ??= [];
        pmc.Hideout.Customization[slot] = entry.CustomizationId.Value;

        return true;
    }

    private async Task NotifyBalanceAsync(MongoId sessionId)
    {
        await NotifyAsync(
            sessionId,
            new WsExpansionsBalance { EventType = NotificationEventType.UpdateAccountTarcoinBalance, Balance = GetBalance(sessionId) }
        );
    }

    private Task NotifyAsync(MongoId sessionId, WsNotificationEvent notification)
    {
        notification.EventIdentifier = new MongoId();

        return notificationSendHelper.SendMessageAsync(sessionId, notification);
    }

    /// <summary>
    ///     Walk a bundle's items down to the offers carrying a template or customisation
    /// </summary>
    /// <param name="offer">Offer being bought</param>
    /// <param name="visitedOfferIds">The offer and every offer inside it</param>
    /// <param name="missingOfferIds">Bundle parts the shop data does not have</param>
    /// <returns>Entries to deliver</returns>
    public List<ShopOfferItem> CollectDeliverables(
        ShopOffer offer,
        out HashSet<string> visitedOfferIds,
        out List<string> missingOfferIds
    )
    {
        var deliverables = new List<ShopOfferItem>();
        var seen = new HashSet<string>();
        var missing = new List<string>();
        var pending = new Queue<ShopOffer>();
        pending.Enqueue(offer);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!seen.Add(current.Id.ToString()))
            {
                continue;
            }

            foreach (var entry in current.Items)
            {
                if (entry.OfferId is null)
                {
                    deliverables.Add(entry);

                    continue;
                }

                var part = GetOffer(entry.OfferId.Value.ToString());
                if (part is null)
                {
                    missing.Add(entry.OfferId.Value.ToString());

                    continue;
                }

                pending.Enqueue(part);
            }
        }

        visitedOfferIds = seen;
        missingOfferIds = missing;

        return deliverables;
    }

    /// <summary>
    ///     Carry Shop purchases from a wiped profile to its replacement
    /// </summary>
    /// <param name="oldProfile">Profile being wiped</param>
    /// <param name="newProfile">Profile replacing it</param>
    public void CarryOverPurchases(SptProfile oldProfile, SptProfile newProfile)
    {
        var oldPmc = oldProfile.CharacterData?.PmcData;
        var newPmc = newProfile.CharacterData?.PmcData;
        if (oldPmc is null || newPmc is null)
        {
            return;
        }

        // A first profile has no balances yet and keeps what its edition starts with
        newPmc.TarCoinBalance = oldPmc.TarCoinBalance ?? newPmc.TarCoinBalance;
        newPmc.BattlePassUniversalDocumentBalance = oldPmc.BattlePassUniversalDocumentBalance ?? newPmc.BattlePassUniversalDocumentBalance;
        newProfile.PurchasedShopOffers = oldProfile.PurchasedShopOffers;
        newProfile.PurchasedShopStashRows = oldProfile.PurchasedShopStashRows;

        newProfile.CustomisationUnlocks ??= [];
        foreach (var offerId in oldProfile.PurchasedShopOffers ?? [])
        {
            var offer = GetOffer(offerId);
            if (offer is null)
            {
                continue;
            }

            foreach (var entry in CollectDeliverables(offer, out _, out _))
            {
                if (
                    entry.CustomizationId is null
                    || newProfile.CustomisationUnlocks.Exists(unlock => Equals(unlock.Id, entry.CustomizationId.Value))
                )
                {
                    continue;
                }

                newProfile.CustomisationUnlocks.Add(
                    new CustomisationStorage
                    {
                        Id = entry.CustomizationId.Value,
                        Source = CustomisationSource.UNLOCKED_IN_GAME,
                        Type = entry.CustomizationType ?? CustomisationType.SUITE,
                    }
                );
            }
        }

        if (newProfile.PurchasedShopStashRows > 0)
        {
            ProfileHelper.AddStashRowsBonusToProfile(newPmc, newProfile.PurchasedShopStashRows.Value);
        }
    }

    /// <summary>
    ///     Count the units of an offer the player owns and still lacks
    /// </summary>
    /// <param name="profile">Buyer's profile</param>
    /// <param name="offer">Offer to count</param>
    /// <returns>Owned units and units still to buy. A bundle has a unit per part it is made of.</returns>
    public (int Purchased, int Available) GetStock(SptProfile? profile, ShopOffer offer)
    {
        var purchased = 0;
        var available = 0;
        foreach (var unit in Units(offer, []))
        {
            if (OwnsUnit(profile, unit))
            {
                purchased++;
            }
            else
            {
                available++;
            }
        }

        return (purchased, available);
    }

    /// <summary>
    ///     Check whether an offer is purchased, which it is once none of its units are left to buy. A bundle
    ///     whose parts were all bought on their own, or through another bundle, counts too
    /// </summary>
    /// <param name="sessionId">Session to check</param>
    /// <param name="offer">Offer to check</param>
    /// <returns>True when nothing of the offer is left to buy</returns>
    public bool HasPurchased(MongoId sessionId, ShopOffer offer)
    {
        if (offer.Countable)
        {
            return false;
        }

        var (purchased, available) = GetStock(saveServer.GetProfile(sessionId), offer);

        return available == 0 && purchased > 0;
    }

    public HashSet<string> GetPurchasedOffers(MongoId sessionId)
    {
        return shopTable.Content.Offers.Where(offer => HasPurchased(sessionId, offer)).Select(offer => offer.Id.ToString()).ToHashSet();
    }

    /// <summary>
    ///     Get the offers a bundle ends in, repeated where parts overlap
    /// </summary>
    /// <param name="offer">Offer to expand</param>
    /// <param name="path">Bundles already being expanded, which stops a loop</param>
    /// <returns>The offer itself when it has no parts</returns>
    private List<ShopOffer> Units(ShopOffer offer, HashSet<string> path)
    {
        var parts = offer.Items.Where(entry => entry.OfferId is not null).ToList();
        if (parts.Count == 0 || !path.Add(offer.Id.ToString()))
        {
            return [offer];
        }

        var units = new List<ShopOffer>();
        foreach (var part in parts)
        {
            var child = GetOffer(part.OfferId!.Value.ToString());
            if (child is not null)
            {
                units.AddRange(Units(child, path));
            }
        }

        path.Remove(offer.Id.ToString());

        return units;
    }

    /// <summary>
    ///     Check whether a player owns a unit, either bought or with every customisation it grants unlocked some other way
    /// </summary>
    /// <param name="profile">Player's profile</param>
    /// <param name="unit">Offer a bundle ends in</param>
    /// <returns>True when owned</returns>
    private static bool OwnsUnit(SptProfile? profile, ShopOffer unit)
    {
        if (profile?.PurchasedShopOffers?.Contains(unit.Id.ToString()) ?? false)
        {
            return true;
        }

        var customisations = unit.Items.Where(entry => entry.CustomizationId is not null).ToList();

        return customisations.Count > 0
            && customisations.Count == unit.Items.Count
            && customisations.All(entry =>
                profile?.CustomisationUnlocks?.Exists(unlock => Equals(unlock.Id, entry.CustomizationId!.Value)) ?? false
            );
    }

    private void RecordPurchase(MongoId sessionId, IEnumerable<string> offerIds)
    {
        var profile = saveServer.GetProfile(sessionId);
        if (profile is null)
        {
            return;
        }

        profile.PurchasedShopOffers ??= [];
        profile.PurchasedShopOffers.UnionWith(offerIds);
    }

    private void SetBalance(MongoId sessionId, int amount)
    {
        var pmc = saveServer.GetProfile(sessionId)?.CharacterData?.PmcData;
        if (pmc is null)
        {
            return;
        }

        pmc.TarCoinBalance = amount;
    }
}
