using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.DI.Routing;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Enums;

namespace SPTarkov.Server.Core.Routers.ItemEvents;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public sealed class BattlePassItemEventRouter(BattlePassCallbacks battlePassCallbacks)
    : ItemEventRouter([
        new ItemRouteAction<BattlePassUnlockRewardRequest>(
            ItemEventActions.BATTLE_PASS_UNLOCK_REWARD,
            async (url, pmcData, body, sessionID, output, cancellationToken) =>
                await battlePassCallbacks.UnlockReward(pmcData, body, sessionID)
        ),
    ])
{ }
