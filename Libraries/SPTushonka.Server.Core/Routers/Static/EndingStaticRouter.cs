using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Routers.Static;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class EndingStaticRouter(JsonUtil jsonUtil, EndingCallbacks endingCallbacks)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<EndingRequest>(
                "/client/ending/obtain",
                async (url, info, sessionID, output, cancellationToken) => await endingCallbacks.ObtainEnding(url, info, sessionID)
            ),
            new RouteAction<EndingRequest>(
                "/client/ending/localization",
                async (url, info, sessionID, output, cancellationToken) => await endingCallbacks.GetLocalization(url, info, sessionID)
            ),
        ]
    )
{ }
