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
    public ValueTask<ItemEventRouterResponse> UnlockReward(
        PmcData pmcData,
        BattlePassUnlockRewardRequest info,
        MongoId sessionID
    )
    {
        return new ValueTask<ItemEventRouterResponse>(battlePassController.UnlockReward(pmcData, info, sessionID));
    }
}
