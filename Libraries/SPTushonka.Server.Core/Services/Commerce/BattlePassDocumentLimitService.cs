using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Services.Commerce;

[Injectable(InjectionType.Singleton)]
public class BattlePassDocumentLimitService(SaveServer saveServer, SeasonTable seasonTable, TimeUtil timeUtil)
{
    private const string PveGameMode = "pve";

    /// <summary>Roll every loaded profile's document allowance forward.</summary>
    public void RefillExpiredBattlePassLimits()
    {
        foreach (var (_, profile) in saveServer.GetProfiles())
        {
            var pmcData = profile.CharacterData?.PmcData;

            if (pmcData is not null)
            {
                RefillExpiredLimits(pmcData);
            }
        }
    }

    /// <summary>
    ///     Top a profile's document allowance back up for every pass whose reset time has passed.
    ///     A full allowance keeps its reset rolling forward.
    /// </summary>
    public void RefillExpiredLimits(PmcData pmcData)
    {
        if (pmcData.BattlePassDocumentLimitData is null || pmcData.BattlePassDocumentLimitData.Count == 0)
        {
            return;
        }

        var now = timeUtil.GetTimeStamp();

        foreach (var (battlePassId, limit) in pmcData.BattlePassDocumentLimitData)
        {
            var totalLimit = GetTotalLimit(battlePassId) ?? limit.TotalLimit;
            if (limit.NextResetTime is null || limit.NextResetTime <= now)
            {
                limit.RemainingLimit = totalLimit;
            }

            limit.TotalLimit = totalLimit;

            // The reset counts from the first document picked up, so a full allowance has not started it yet
            if (limit.RemainingLimit >= totalLimit)
            {
                limit.NextResetTime = now + (limit.ResetInterval ?? 0);
            }
        }
    }

    /// <summary>
    ///     Charge the season documents a raid added to the player's equipment against each pass's allowance.
    /// </summary>
    public void ConsumeRaidDocuments(PmcData serverProfile, PmcData postRaidProfile)
    {
        if (serverProfile.BattlePassDocumentLimitData is null)
        {
            return;
        }

        foreach (var (battlePassId, limit) in serverProfile.BattlePassDocumentLimitData)
        {
            var documents = seasonTable
                .BattlePass?.BattlePasses?.FirstOrDefault(pass => pass.Id == battlePassId)
                ?.Documents?.Select(document => document.ItemId)
                .ToHashSet();
            if (documents is null || documents.Count == 0)
            {
                continue;
            }

            var gained = CountEquippedDocuments(postRaidProfile, documents) - CountEquippedDocuments(serverProfile, documents);
            if (gained <= 0)
            {
                continue;
            }

            if (limit.RemainingLimit >= limit.TotalLimit)
            {
                limit.NextResetTime = timeUtil.GetTimeStamp() + (limit.ResetInterval ?? 0);
            }

            limit.RemainingLimit = Math.Max(0, (limit.RemainingLimit ?? 0) - gained);
        }
    }

    private static int CountEquippedDocuments(PmcData profile, HashSet<MongoId> documents)
    {
        if (profile.Inventory?.Items is null || profile.Inventory.Equipment is null)
        {
            return 0;
        }

        return profile
            .Inventory.Items.GetItemWithChildren(profile.Inventory.Equipment.Value)
            .Where(item => documents.Contains(item.Template))
            .Sum(item => item.GetItemStackSize());
    }

    private int? GetTotalLimit(MongoId battlePassId)
    {
        var battlePass = seasonTable.BattlePass?.BattlePasses?.FirstOrDefault(pass => pass.Id == battlePassId);

        return battlePass
            ?.DocumentLimits?.LimitsByGameMode?.FirstOrDefault(limit =>
                string.Equals(limit.GameMode, PveGameMode, StringComparison.OrdinalIgnoreCase)
            )
            ?.TotalLimit;
    }
}
