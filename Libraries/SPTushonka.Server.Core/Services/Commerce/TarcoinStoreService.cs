using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;

namespace SPTarkov.Server.Core.Services.Commerce;

[Injectable(InjectionType.Singleton)]
public class TarcoinStoreService(
    ISptLogger<TarcoinStoreService> logger,
    SaveServer saveServer,
    ShopTable shopTable,
    MailSendService mailSendService
)
{
    public int GetBalance(MongoId sessionId)
    {
        return saveServer.GetProfile(sessionId)?.CharacterData?.PmcData?.TarCoinBalance ?? 0;
    }

    public void Credit(MongoId sessionId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetBalance(sessionId, GetBalance(sessionId) + amount);
    }

    /// <summary>
    ///     Take <paramref name="cost" /> from the wallet when it covers it.
    /// </summary>
    /// <returns>False when the balance is short, leaving it untouched</returns>
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

    /// <summary>
    ///     Build the reply the shop webview expects from /v2/shop/api/v1/account/balance/.
    /// </summary>
    public ShopBalanceResponse GetBalanceResponse(MongoId sessionId)
    {
        return new ShopBalanceResponse
        {
            Data = new ShopBalanceData { Item = new ShopBalanceItem { Balance = GetBalance(sessionId) } },
        };
    }


    /// <summary>The tab list behind /v2/shop/api/v1/menu.</summary>
    public List<ShopMenuItem> GetMenu()
    {
        return shopTable.Content.Menu.OrderBy(item => item.Order).ToList();
    }

    /// <summary>A page of offer tiles, behind /v2/shop/api/v1/page/{id}.</summary>
    public ShopPage? GetPage(string pageId)
    {
        return shopTable.Content.Pages.FirstOrDefault(page => page.Id.ToString() == pageId);
    }

    /// <summary>One offer's detail, behind /v2/shop/api/v1/catalog/{id}.</summary>
    public ShopOffer? GetOffer(string offerId)
    {
        return shopTable.Content.Offers.FirstOrDefault(offer => offer.Id.ToString() == offerId);
    }

    public List<ShopPrice> GetPrices(IEnumerable<string> offerIds)
    {
        var wanted = offerIds.ToHashSet();

        return shopTable.Content.Prices.Where(price => wanted.Contains(price.Id.ToString())).ToList();
    }

    /// <summary>Resolve a display key such as "offer.&lt;id&gt;.name" for a language.</summary>
    public string Localise(string? key, string language = "en")
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        // A language can be missing a string the shop still has in english, so fall through rather
        // than showing the raw key.
        if (shopTable.Content.Locale.TryGetValue(language, out var strings)
            && strings.TryGetValue(key, out var value)
            && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        return shopTable.Content.Locale.TryGetValue("en", out var english) && english.TryGetValue(key, out var fallback)
            ? fallback
            : key;
    }

    public async Task<bool> TryPurchaseAsync(MongoId sessionId, string offerId, int count, CancellationToken cancellationToken = default)
    {
        var offer = GetOffer(offerId);
        var price = shopTable.Content.Prices.FirstOrDefault(entry => entry.Id.ToString() == offerId);

        if (offer is null || price is null)
        {
            logger.Warning($"TarCoin purchase refused, unknown or unpriced offer {offerId}");

            return false;
        }

        var quantity = count < 1 ? 1 : count;

        if (!offer.Countable && HasPurchased(sessionId, offerId))
        {
            logger.Warning($"TarCoin purchase refused, offer {offerId} has already been bought");

            return false;
        }

        var deliverables = CollectDeliverables(offer, out var boughtOfferIds);
        if (deliverables.Count == 0)
        {
            logger.Warning($"TarCoin purchase refused, offer {offerId} has nothing to deliver");

            return false;
        }

        if (!TrySpend(sessionId, price.AmountWithDiscount * quantity))
        {
            return false;
        }

        var items = new List<Item>();
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
                    pmc.BattlePassUniversalDocumentBalance =
                        (pmc.BattlePassUniversalDocumentBalance ?? 0) + entry.Quantity * quantity;
                }

                continue;
            }

            if (entry.CustomizationId is null)
            {
                logger.Warning($"Offer {offerId} carries a {entry.Type} entry that cannot be delivered");

                continue;
            }

            profile.CustomisationUnlocks ??= [];
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
        
        await saveServer.SaveProfileAsync(sessionId, cancellationToken);

        return true;
    }

    /// <summary>
    ///     Everything an offer hands over. A bundle's contents are other offers, possibly bundles
    ///     themselves, so the tree is walked down to the offers carrying a template or customisation.
    /// </summary>
    private List<ShopOfferItem> CollectDeliverables(ShopOffer offer, out HashSet<string> visitedOfferIds)
    {
        var deliverables = new List<ShopOfferItem>();
        var seen = new HashSet<string>();
        var pending = new Queue<ShopOffer>();
        pending.Enqueue(offer);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!seen.Add(current.Id.ToString()))
            {
                continue;
            }

            deliverables.AddRange(current.Items);

            foreach (var part in current.RelatedOffers)
            {
                var child = GetOffer(part.Id.ToString());
                if (child is not null)
                {
                    pending.Enqueue(child);
                }
            }
        }

        visitedOfferIds = seen;

        return deliverables;
    }

    /// <summary>Has this account already bought a one-off offer.</summary>
    public bool HasPurchased(MongoId sessionId, string offerId)
    {
        return saveServer.GetProfile(sessionId)?.PurchasedShopOffers?.Contains(offerId) ?? false;
    }

    /// <summary>Every one-off offer this account has bought, for greying the cards out.</summary>
    public HashSet<string> GetPurchasedOffers(MongoId sessionId)
    {
        return saveServer.GetProfile(sessionId)?.PurchasedShopOffers ?? [];
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
