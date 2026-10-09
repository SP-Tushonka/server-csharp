using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.DI.Routing;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;

namespace SPTarkov.Server.Core.Routers.ItemEvents;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public sealed class EndingItemEventRouter(EndingCallbacks endingCallbacks)
    : ItemEventRouter([
        new ItemRouteAction<SetDisplayedEndingRequest>(
            ItemEventActions.SET_CURRENT_ENDING,
            async (url, pmcData, body, sessionID, output, cancellationToken) =>
                await endingCallbacks.SetDisplayedEnding(pmcData, body, sessionID)
        ),
    ]) { }
