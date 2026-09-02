using SPTarkov.DI.Annotations;
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
            if (limit.NextResetTime > now)
            {
                continue;
            }

            var totalLimit = GetTotalLimit(battlePassId) ?? limit.TotalLimit;
            var resetInterval = limit.ResetInterval ?? 0;
            var nextReset = resetInterval > 0 ? limit.NextResetTime!.Value : now;

            while (resetInterval > 0 && nextReset <= now)
            {
                nextReset += resetInterval;
            }

            limit.RemainingLimit = totalLimit;
            limit.TotalLimit = totalLimit;
            limit.NextResetTime = nextReset;
        }
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
