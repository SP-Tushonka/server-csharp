using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class TradeController(
    ISptLogger<TradeController> logger,
    TradersTable traderTable,
    EventOutputHolder eventOutputHolder,
    TradeHelper tradeHelper,
    TimeUtil timeUtil,
    RandomUtil randomUtil,
    ItemHelper itemHelper,
    RagfairOfferService ragfairOfferService,
    RagfairOfferHelper ragfairOfferHelper,
    RagfairLevelService ragfairLevelService,
    HttpResponseUtil httpResponseUtil,
    ServerLocalisationService serverLocalisationService,
    MailSendService mailSendService,
    RagfairConfig ragfairConfig,
    TraderConfig traderConfig,
    ProfileHelper profileHelper,
    TraderHelper traderHelper,
    FenceService fenceService,
    RagfairPriceService ragfairPriceService,
    GlobalTable globalTable
)
{
    /// <summary>
    ///     Handle TradingConfirm event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="request"></param>
    /// <param name="sessionID">Session/Player id</param>
    /// <returns></returns>
    public ItemEventRouterResponse ConfirmTrading(PmcData pmcData, ProcessBaseTradeRequestData request, MongoId sessionID)
    {
        var output = eventOutputHolder.GetOutput(sessionID);

        // Buying
        if (request.Type == "buy_from_trader")
        {
            var foundInRaid = traderConfig.PurchasesAreFoundInRaid;
            var buyData = (ProcessBuyTradeRequestData)request;
            tradeHelper.BuyItem(pmcData, buyData, sessionID, foundInRaid, output);

            return output;
        }

        // Selling
        if (request.Type == "sell_to_trader")
        {
            var sellData = (ProcessSellTradeRequestData)request;
            tradeHelper.SellItem(pmcData, pmcData, sellData, sessionID, output);

            return output;
        }

        var errorMessage = $"Unhandled trade event: {request.Type}";
        logger.Error(errorMessage);

        return httpResponseUtil.AppendErrorToOutput(output, errorMessage, BackendErrorCodes.RagfairUnavailable);
    }

    /// <summary>
    ///     Handle RagFairBuyOffer event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="request"></param>
    /// <param name="sessionID">Session/Player id</param>
    /// <returns></returns>
    public ItemEventRouterResponse ConfirmRagfairTrading(PmcData pmcData, ProcessRagfairTradeRequestData request, MongoId sessionID)
    {
        var output = eventOutputHolder.GetOutput(sessionID);

        foreach (var offer in request.Offers)
        {
            var fleaOffer = ragfairOfferService.GetOfferByOfferId(new MongoId(offer.Id));
            if (fleaOffer is null)
            {
                return httpResponseUtil.AppendErrorToOutput(
                    output,
                    $"Offer with ID: {offer.Id} not found",
                    BackendErrorCodes.OfferNotFound
                );
            }

            if (offer.Count == 0)
            {
                var errorMessage = serverLocalisationService.GetText(
                    "ragfair-unable_to_purchase_0_count_item",
                    itemHelper.GetItem(fleaOffer.Items[0].Template).Value.Name
                );
                return httpResponseUtil.AppendErrorToOutput(output, errorMessage, BackendErrorCodes.OfferOutOfStock);
            }

            if (
                !fleaOffer.IsTraderOffer()
                && ragfairLevelService.IsLocked(fleaOffer.Items, pmcData.Info.Level.GetValueOrDefault(0), out var requiredLevel)
            )
            {
                var errorMessage = serverLocalisationService.GetText("ragfair-offer_locked_by_level", requiredLevel);
                return httpResponseUtil.AppendErrorToOutput(output, errorMessage, BackendErrorCodes.RagfairUnavailable);
            }

            if (fleaOffer.IsTraderOffer())
            {
                BuyTraderItemFromRagfair(sessionID, pmcData, fleaOffer, offer, output);
            }
            else
            {
                BuyPmcItemFromRagfair(sessionID, pmcData, fleaOffer, offer, output);
            }

            // Exit loop early if problem found
            if (output.Warnings?.Count > 0)
            {
                return output;
            }
        }

        return output;
    }

    /// <summary>
    ///     Buy an item off the flea sold by a trader
    /// </summary>
    /// <param name="sessionId">Session id</param>
    /// <param name="pmcData">Player profile</param>
    /// <param name="fleaOffer">Offer being purchased</param>
    /// <param name="requestOffer">request data from client</param>
    /// <param name="output">Output to send back to client</param>
    protected void BuyTraderItemFromRagfair(
        MongoId sessionId,
        PmcData pmcData,
        RagfairOffer fleaOffer,
        OfferRequest requestOffer,
        ItemEventRouterResponse output
    )
    {
        // Skip buying items when player doesn't have needed loyalty
        if (!pmcData.ProfileMeetsTraderLoyaltyLevelToBuyOffer(fleaOffer))
        {
            var errorMessage =
                $"Unable to buy item: {fleaOffer.Items[0].Template} from trader: {fleaOffer.User.Id} as loyalty level too low, skipping";
            if (logger.IsLogEnabled(LogLevel.Debug))
            {
                logger.Debug(errorMessage);
            }

            httpResponseUtil.AppendErrorToOutput(output, errorMessage, BackendErrorCodes.RagfairUnavailable);

            return;
        }

        // Trigger purchase of item from trader
        var buyData = new ProcessBuyTradeRequestData
        {
            Action = "TradingConfirm",
            Type = "buy_from_ragfair_trader",
            TransactionId = fleaOffer.User.Id,
            ItemId = fleaOffer.Root,
            Count = requestOffer.Count,
            SchemeId = 0,
            SchemeItems = requestOffer.Items,
        };
        tradeHelper.BuyItem(pmcData, buyData, sessionId, traderConfig.PurchasesAreFoundInRaid, output);

        // Remove/lower offer quantity of item purchased from trader flea offer
        ragfairOfferService.ReduceOfferQuantity(fleaOffer.Id, requestOffer.Count ?? 0);
    }

    /// <summary>
    ///     Buy an item off the flea sold by a PMC
    /// </summary>
    /// <param name="sessionId">Session id</param>
    /// <param name="pmcData">Player profile</param>
    /// <param name="fleaOffer">Offer being purchased</param>
    /// <param name="requestOffer">request data from client</param>
    /// <param name="output">Output to send back to client</param>
    protected void BuyPmcItemFromRagfair(
        MongoId sessionId,
        PmcData pmcData,
        RagfairOffer fleaOffer,
        OfferRequest requestOffer,
        ItemEventRouterResponse output
    )
    {
        var buyData = new ProcessBuyTradeRequestData
        {
            Action = "TradingConfirm",
            Type = "buy_from_ragfair_pmc",
            TransactionId = fleaOffer.User.Id,
            ItemId = fleaOffer.Id, // Store ragfair offerId in buyRequestData.item_id
            Count = requestOffer.Count,
            SchemeId = 0,
            SchemeItems = requestOffer.Items,
        };

        // buyItem() must occur prior to removing the offer stack, otherwise item inside offer doesn't exist for confirmTrading() to use
        tradeHelper.BuyItem(pmcData, buyData, sessionId, ragfairConfig.Dynamic.PurchasesAreFoundInRaid, output);
        if (output.Warnings?.Count > 0)
        {
            return;
        }

        // resolve when a profile buy another profile's offer
        var offerOwnerId = fleaOffer.User.Id;
        var offerBuyCount = requestOffer.Count;

        if (fleaOffer.IsPlayerOffer())
        {
            // Complete selling the offer now it has been purchased
            ragfairOfferHelper.CompleteOffer(offerOwnerId, fleaOffer, offerBuyCount ?? 0);

            return;
        }

        // Remove/lower offer quantity of item purchased from PMC flea offer
        ragfairOfferService.ReduceOfferQuantity(fleaOffer.Id, requestOffer.Count ?? 0);
    }

    /// <summary>
    ///     Handle SellAllFromSavage event
    /// </summary>
    /// <param name="pmcData">Players PMC profile</param>
    /// <param name="request"></param>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns></returns>
    public ItemEventRouterResponse SellScavItemsToFence(PmcData pmcData, SellScavItemsToFenceRequestData request, MongoId sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);
        var scavInventory = profileHelper.GetScavProfile(sessionId)?.Inventory;
        if (scavInventory?.Items is null)
        {
            return output;
        }

        var fence = traderTable.GetTrader(Traders.FENCE).Base;
        var prices = ragfairPriceService.GetAllStaticPrices();
        var sellModifier = Math.Round(1 - traderHelper.GetLoyaltyLevel(Traders.FENCE, pmcData).BuyPriceCoefficient / 100, 3);
        var fenceModifier = Math.Round(fenceService.GetFenceInfo(pmcData)?.PriceModifier ?? 1, 3);

        var total = 0;
        foreach (var item in GetScavItemsToSell(scavInventory).ToList())
        {
            var itemWithChildren = scavInventory.Items.GetItemWithChildren(item.Id);
            total += GetScavSellPrice(itemWithChildren, fence, prices, sellModifier, fenceModifier);
            scavInventory.Items.RemoveAll(itemWithChildren.Contains);
        }

        if (total > 0)
        {
            MailMoneyToPlayer(sessionId, total, Traders.FENCE);
        }

        return output;
    }

    /// <summary>
    ///     Get the scav's items the client sells to Fence. Everything in an equipment slot plus the pocket contents,
    ///     except the pockets' special slots
    /// </summary>
    /// <param name="inventory">Scav inventory</param>
    /// <returns>Root items to sell</returns>
    protected IEnumerable<Item> GetScavItemsToSell(BotBaseInventory inventory)
    {
        foreach (var item in inventory.Items.Where(item => item.ParentId == inventory.Equipment))
        {
            if (!itemHelper.GetItem(item.Template).Value?.Properties?.NotShownInSlot ?? true)
            {
                yield return item;
                continue;
            }

            foreach (var pocketItem in inventory.Items.Where(child => child.ParentId == item.Id))
            {
                if (pocketItem.SlotId?.StartsWith("SpecialSlot", StringComparison.Ordinal) != true)
                {
                    yield return pocketItem;
                }
            }
        }
    }

    /// <summary>
    ///     Get the roubles Fence pays for an item and its children when a scav sells everything
    /// </summary>
    /// <param name="itemWithChildren">Item to sell and its children</param>
    /// <param name="fence">Fence's trader base</param>
    /// <param name="prices">Handbook prices</param>
    /// <param name="sellModifier">Fence loyalty level sell modifier</param>
    /// <param name="fenceModifier">Fence standing price modifier</param>
    /// <returns>Rouble price</returns>
    protected int GetScavSellPrice(
        List<Item> itemWithChildren,
        TraderBase fence,
        Dictionary<MongoId, double> prices,
        double sellModifier,
        double fenceModifier
    )
    {
        var price = 0d;
        var roubles = 0;
        foreach (var item in itemWithChildren)
        {
            if (item.Template == Money.ROUBLES)
            {
                roubles += (int)(item.Upd?.StackObjectsCount ?? 1);
                continue;
            }

            var itemPrice = GetScavSellBasePrice(item, prices);
            if (!FenceBuysItem(item.Template, fence))
            {
                itemPrice *= (fence.ProhibitedItemsSellModifier ?? 0) / 100f;
            }

            price += itemPrice;
        }

        price = Math.Floor(Math.Floor(price * sellModifier) / fenceModifier);

        return (int)Math.Floor(price + roubles);
    }

    /// <summary>
    ///     Check whether Fence buys an item template outright
    /// </summary>
    /// <param name="template">Item template</param>
    /// <param name="fence">Fence's trader base</param>
    /// <returns>True when the template is bought and not prohibited</returns>
    protected bool FenceBuysItem(MongoId template, TraderBase fence)
    {
        return !MatchesBuyData(template, fence.ItemsBuyProhibited) && MatchesBuyData(template, fence.ItemsBuy);
    }

    /// <summary>
    ///     Check whether an item template is listed in a trader's buy data by id or category
    /// </summary>
    /// <param name="template">Item template</param>
    /// <param name="buyData">Trader buy or prohibited buy data</param>
    /// <returns>True when listed</returns>
    protected bool MatchesBuyData(MongoId template, ItemBuyData? buyData)
    {
        return buyData is not null && (buyData.IdList.Contains(template) || itemHelper.IsOfBaseclasses(template, buyData.Category));
    }

    /// <summary>
    ///     Get an item's handbook price adjusted for its condition, the way the client values items sold to a trader
    /// </summary>
    /// <param name="item">Item to price</param>
    /// <param name="prices">Handbook prices</param>
    /// <returns>Rouble price for the whole stack</returns>
    protected double GetScavSellBasePrice(Item item, Dictionary<MongoId, double> prices)
    {
        if (!prices.TryGetValue(item.Template, out var price) || price < float.Epsilon)
        {
            return 0;
        }

        var properties = itemHelper.GetItem(item.Template).Value?.Properties;
        var upd = item.Upd;
        if (properties is null || upd is null)
        {
            return price;
        }

        if (upd.Repairable is not null && properties.Durability > 0)
        {
            var maxDurability = Math.Ceiling(upd.Repairable.MaxDurability ?? 0);
            var repairCost = properties.RepairCost * (maxDurability - Math.Ceiling(upd.Repairable.Durability ?? 0));
            price = price * (maxDurability / properties.Durability.Value + (maxDurability == 0 ? 0.01 : 0)) - repairCost;
        }

        if (upd.Buff?.Value is not null)
        {
            var enhancements = globalTable.Configuration.RepairSettings.ItemEnhancementSettings;
            var modifier = upd.Buff.BuffType switch
            {
                RepairBuffType.DamageReduction => enhancements.DamageReduction.PriceModifierValue,
                RepairBuffType.MalfunctionProtections => enhancements.MalfunctionProtections.PriceModifierValue,
                RepairBuffType.WeaponSpread => enhancements.WeaponSpread.PriceModifierValue,
                _ => 0d,
            };
            price *= 1 + Math.Abs(upd.Buff.Value.Value - 1) * modifier;
        }

        if (upd.Dogtag is not null)
        {
            price *= upd.Dogtag.Level ?? 0;
        }

        if (upd.Key is not null && properties.MaximumNumberOfUsage > 0)
        {
            var maxUsages = properties.MaximumNumberOfUsage.Value;
            price = price / maxUsages * (maxUsages - (upd.Key.NumberOfUsages ?? 0));
        }

        if (upd.Resource?.Value is not null && properties.MaxResource > 0)
        {
            price = price * 0.1 + price * 0.9 / properties.MaxResource.Value * upd.Resource.Value.Value;
        }

        if (upd.SideEffect?.Value is not null && properties.MaxResource > 0)
        {
            price = price * 0.1 + price * 0.9 / properties.MaxResource.Value * upd.SideEffect.Value.Value;
        }

        if (upd.MedKit?.HpResource is not null && properties.MaxHpResource > 0)
        {
            price = price / properties.MaxHpResource.Value * upd.MedKit.HpResource.Value;
        }

        if (upd.FoodDrink?.HpPercent is not null && properties.MaxResource > 0)
        {
            price = price / properties.MaxResource.Value * upd.FoodDrink.HpPercent.Value;
        }

        if (upd.RepairKit?.Resource is not null && properties.MaxRepairResource > 0)
        {
            price = price / properties.MaxRepairResource.Value * Math.Max(upd.RepairKit.Resource.Value, 1);
        }

        return price * (upd.StackObjectsCount ?? 1);
    }

    /// <summary>
    ///     Send the specified rouble total to player as mail
    /// </summary>
    /// <param name="sessionId">Session id</param>
    /// <param name="roublesToSend">amount of roubles to send</param>
    /// <param name="trader">Trader to sell items to</param>
    protected void MailMoneyToPlayer(MongoId sessionId, int roublesToSend, MongoId trader)
    {
        if (logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"Selling scav items to fence for {roublesToSend} roubles");
        }

        // Create single currency item with all currency on it
        var rootCurrencyReward = new Item
        {
            Id = new MongoId(),
            Template = Money.ROUBLES,
            Upd = new Upd { StackObjectsCount = roublesToSend },
        };

        // Ensure money is properly split to follow its max stack size limit
        var currencyReward = itemHelper.SplitStackIntoSeparateItems(rootCurrencyReward);

        // Send mail from trader
        mailSendService.SendLocalisedNpcMessageToPlayer(
            sessionId,
            trader,
            MessageType.MessageWithItems,
            randomUtil.GetArrayValue(traderTable.GetTrader(trader).Dialogue.TryGetValue("soldItems", out var items) ? items : []),
            currencyReward.SelectMany(x => x).ToList(),
            timeUtil.GetHoursAsSeconds(72)
        );
    }
}
