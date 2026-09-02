using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Routers.Static;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class QuestStaticRouter(JsonUtil jsonUtil, QuestCallbacks questCallbacks)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/client/quest/chains",
                async (url, info, sessionID, output, cancellationToken) => await questCallbacks.GetQuestChains(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/completable-item/quests/list",
                async (url, info, sessionID, output, cancellationToken) => await questCallbacks.GetCompletableItemQuests(url, info, sessionID)
            ),
            new RouteAction<CompleteStoryQuestRequest>(
                "/client/quest/complete",
                async (url, info, sessionID, output, cancellationToken) => await questCallbacks.CompleteQuest(url, info, sessionID)
            ),
            new StreamedRouteAction<ListQuestsRequestData>(
                "/client/quest/list",
                async (url, info, sessionID, cancellationToken) => await questCallbacks.ListQuests(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                //Yes the typo is intended, BSG has it in the live client as well and it has to match
                "/client/repeatalbeQuests/activityPeriods",
                async (url, info, sessionID, output, cancellationToken) => await questCallbacks.ActivityPeriods(url, info, sessionID)
            ),
        ]
    )
{ }
