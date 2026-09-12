using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class BattlePassController(
    ISptLogger<BattlePassController> logger,
    SeasonTable seasonTable,
    ProfileHelper profileHelper,
    InventoryHelper inventoryHelper,
    ItemHelper itemHelper,
    RewardHelper rewardHelper,
    ServerLocalisationService serverLocalisationService,
    MailSendService mailSendService,
    HttpResponseUtil httpResponseUtil,
    EventOutputHolder eventOutputHolder
)
{
    /// <summary>
    ///     Claim a reward from the season pass: charge its cost in documents, record it against the pass
    ///     so it cannot be claimed twice, then hand over what it contains.
    /// </summary>
    public ItemEventRouterResponse UnlockReward(PmcData pmcData, BattlePassUnlockRewardRequest request, MongoId sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);

        var pass = seasonTable.BattlePass.BattlePasses?.FirstOrDefault(entry => entry.Id == request.BattlePassId);
        var reward = pass?.Pages?.SelectMany(page => page.Rewards ?? []).FirstOrDefault(entry => entry.Id == request.RewardId);
        if (pass is null || reward is null)
        {
            logger.Error($"Season reward {request.RewardId} is not part of battle pass {request.BattlePassId}");

            return httpResponseUtil.AppendErrorToOutput(output, "Unknown season reward");
        }

        var progress = GetProgress(pmcData, pass);
        if (progress.ObtainedRewardIds!.Contains(request.RewardId))
        {
            logger.Warning($"Season reward {request.RewardId} has already been claimed");

            return output;
        }

        var problem = FindPaymentProblem(pmcData, pass, reward, request);
        if (problem is not null)
        {
            logger.Warning($"Season reward {request.RewardId} refused: {problem}");

            return httpResponseUtil.AppendErrorToOutput(output, problem);
        }

        foreach (var handIn in request.Items ?? [])
        {
            inventoryHelper.RemoveItemByCount(pmcData, handIn.Id, handIn.Count, sessionId, output);
        }

        pmcData.BattlePassUniversalDocumentBalance = (pmcData.BattlePassUniversalDocumentBalance ?? 0) - request.UniversalDocuments;

        progress.ObtainedRewardIds!.Add(request.RewardId);
        progress.Completed = progress.ObtainedRewardIds!.Count;

        var fullProfile = profileHelper.GetFullProfile(sessionId);
        if (fullProfile is not null)
        {
            var rewardItems = rewardHelper.ApplyRewards(
                reward.Rewards ?? [],
                CustomisationSource.UNLOCKED_IN_GAME,
                fullProfile,
                pmcData,
                request.RewardId,
                output
            );

            if (rewardItems.Count > 0)
            {
                mailSendService.SendSystemMessageToPlayer(sessionId, "Season pass reward", rewardItems);
            }
        }

        return output;
    }

    /// <summary>Trade a number of season documents for one of another kind, at the pass's exchange rate.</summary>
    public ItemEventRouterResponse ExchangeDocuments(PmcData pmcData, BattlePassExchangeDocumentsRequest request, MongoId sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);

        var pass = seasonTable.BattlePass.BattlePasses?.FirstOrDefault(entry => entry.Id == request.BattlePassId);
        var received = pass?.Documents?.FirstOrDefault(document => document.Id == request.ReceiveDocumentId);
        if (pass?.ExchangeRate is null || received is null)
        {
            logger.Error($"Battle pass {request.BattlePassId} has no document {request.ReceiveDocumentId} to exchange for");

            return httpResponseUtil.AppendErrorToOutput(output, "Unknown season document");
        }

        return Exchange(pmcData, pass, request, received.ItemId, pass.ExchangeRate.Value, sessionId, output);
    }

    /// <summary>Trade season documents for the pass's container, at the document price the pass sets.</summary>
    public ItemEventRouterResponse ExchangeDocumentsForItem(PmcData pmcData, BattlePassExchangeDocumentsRequest request, MongoId sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);

        var pass = seasonTable.BattlePass.BattlePasses?.FirstOrDefault(entry => entry.Id == request.BattlePassId);
        var settings = pass?.ItemExchangeSettings;
        if (pass is null || settings?.RequiredDocuments is null)
        {
            logger.Error($"Battle pass {request.BattlePassId} has no item to exchange documents for");

            return httpResponseUtil.AppendErrorToOutput(output, "Unknown season item");
        }

        return Exchange(pmcData, pass, request, settings.ItemId, settings.RequiredDocuments.Value, sessionId, output);
    }

    /// <summary>
    ///     Take the handed in documents and put what they buy in the stash.
    /// </summary>
    private ItemEventRouterResponse Exchange(
        PmcData pmcData,
        BattlePass pass,
        BattlePassExchangeDocumentsRequest request,
        MongoId receivedTemplate,
        int price,
        MongoId sessionId,
        ItemEventRouterResponse output
    )
    {
        var total = (request.Items ?? []).Sum(handIn => handIn.Count);
        var problem = FindHandInProblem(pmcData, pass, request.Items);
        if (problem is null && (total <= 0 || total % price != 0))
        {
            problem = $"Incorrect number of documents to hand over ({total}/{price})";
        }

        if (problem is not null)
        {
            logger.Warning($"Season document exchange refused: {problem}");

            return httpResponseUtil.AppendErrorToOutput(output, problem);
        }

        var received = ReceivedItems(receivedTemplate, total / price);
        if (!inventoryHelper.CanPlaceItemsInInventory(sessionId, received))
        {
            return httpResponseUtil.AppendErrorToOutput(
                output,
                serverLocalisationService.GetText("inventory-no_stash_space"),
                BackendErrorCodes.NotEnoughSpace
            );
        }

        foreach (var handIn in request.Items ?? [])
        {
            inventoryHelper.RemoveItemByCount(pmcData, handIn.Id, handIn.Count, sessionId, output);
        }

        var addRequest = new AddItemsDirectRequest
        {
            ItemsWithModsToAdd = received,
            FoundInRaid = true,
            UseSortingTable = false,
            Callback = null,
        };
        inventoryHelper.AddItemsToStash(sessionId, addRequest, pmcData, output);

        return output;
    }

    // Documents stack and the container does not, so the count is split by what the template allows
    private List<List<Item>> ReceivedItems(MongoId template, int count)
    {
        var stackSize = Math.Max(itemHelper.GetItem(template).Value?.Properties?.StackMaxSize ?? 1, 1);
        var stacks = new List<List<Item>>();
        for (var remaining = count; remaining > 0; remaining -= stackSize)
        {
            stacks.Add([
                new Item
                {
                    Id = new MongoId(),
                    Template = template,
                    Upd = new Upd { StackObjectsCount = Math.Min(remaining, stackSize) },
                },
            ]);
        }

        return stacks;
    }

    /// <summary>Every handed in stack must be a season document the profile owns in that quantity.</summary>
    /// <returns>Null when they all are, otherwise why not</returns>
    private static string? FindHandInProblem(PmcData pmcData, BattlePass pass, List<BattlePassHandIn>? handIns)
    {
        foreach (var handIn in handIns ?? [])
        {
            var item = pmcData.Inventory?.Items?.FirstOrDefault(entry => entry.Id == handIn.Id);
            if (item is null || handIn.Count > item.GetItemStackSize())
            {
                return "Handed in document is not in the profile";
            }

            if (!(pass.Documents ?? []).Any(document => document.ItemId == item.Template))
            {
                return "Handed in item is not a season document";
            }
        }

        return null;
    }

    /// <summary>
    ///     Check the claim is paid for. Cost is per document, and each scheme is satisfied either
    ///     by handing that scheme's document in or by topping up from the universal balance 1:1.
    /// </summary>
    /// <returns>Null when the claim is covered, otherwise why it is not</returns>
    private static string? FindPaymentProblem(
        PmcData pmcData,
        BattlePass pass,
        BattlePassPageReward reward,
        BattlePassUnlockRewardRequest request
    )
    {
        if (request.UniversalDocuments < 0 || request.UniversalDocuments > (pmcData.BattlePassUniversalDocumentBalance ?? 0))
        {
            return "Not enough documents";
        }

        var handInProblem = FindHandInProblem(pmcData, pass, request.Items);
        if (handInProblem is not null)
        {
            return handInProblem;
        }

        var handedIn = new Dictionary<MongoId, int>();
        foreach (var handIn in request.Items ?? [])
        {
            var template = pmcData.Inventory!.Items!.First(entry => entry.Id == handIn.Id).Template;
            handedIn.TryGetValue(template, out var running);
            handedIn[template] = running + handIn.Count;
        }

        var universalRemaining = request.UniversalDocuments;

        foreach (var (schemeId, required) in reward.Cost ?? [])
        {
            var template = (pass.Documents ?? []).FirstOrDefault(document => document.Id == schemeId)?.ItemId;
            if (template is null)
            {
                return "Reward costs a document the pass does not define";
            }

            handedIn.TryGetValue(template.Value, out var available);

            var paidWithDocuments = Math.Min(required, available);
            handedIn[template.Value] = available - paidWithDocuments;

            universalRemaining -= required - paidWithDocuments;
            if (universalRemaining < 0)
            {
                return "Not enough documents";
            }
        }

        return null;
    }

    /// <summary>The profile's progress for the battlepass, created on first claim.</summary>
    private ProfileBattlePassProgress GetProgress(PmcData pmcData, BattlePass pass)
    {
        pmcData.BattlePassProgress ??= [];

        var progress = pmcData.BattlePassProgress.FirstOrDefault(entry => entry.BattlePassId == pass.Id);
        if (progress is null)
        {
            progress = new ProfileBattlePassProgress { BattlePassId = pass.Id, Completed = 0 };
            pmcData.BattlePassProgress.Add(progress);
        }

        progress.ObtainedRewardIds ??= [];
        progress.Total = (pass.Pages ?? []).Sum(page => page.Rewards?.Count ?? 0);

        return progress;
    }
}
