using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable]
public class BattlePassCallbacks(BattlePassController battlePassController)
{
    /// <summary>
    ///     Handle BattlePassUnlockReward event
    /// </summary>
    public ValueTask<ItemEventRouterResponse> UnlockReward(PmcData pmcData, BattlePassUnlockRewardRequest info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(battlePassController.UnlockReward(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle BattlePassExchangeDocuments event
    /// </summary>
    public ValueTask<ItemEventRouterResponse> ExchangeDocuments(PmcData pmcData, BattlePassExchangeDocumentsRequest info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(battlePassController.ExchangeDocuments(pmcData, info, sessionID));
    }

    /// <summary>
    ///     Handle BattlePassExchangeDocumentsForItem event
    /// </summary>
    public ValueTask<ItemEventRouterResponse> ExchangeDocumentsForItem(
        PmcData pmcData,
        BattlePassExchangeDocumentsRequest info,
        MongoId sessionID
    )
    {
        return new ValueTask<ItemEventRouterResponse>(battlePassController.ExchangeDocumentsForItem(pmcData, info, sessionID));
    }
}
