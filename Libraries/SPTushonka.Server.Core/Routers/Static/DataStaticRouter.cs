using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Routers.Static;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class DataStaticRouter(JsonUtil jsonUtil, DataCallbacks dataCallbacks)
    : StaticRouter(
        jsonUtil,
        [
            new StreamedRouteAction<EmptyRequestData>(
                "/client/settings",
                async (url, info, sessionID, cancellationToken) => await dataCallbacks.GetSettings(url, info, sessionID)
            ),
            new StreamedRouteAction<EmptyRequestData>(
                "/client/globals",
                async (url, info, sessionID, cancellationToken) => await dataCallbacks.GetGlobals(url, info, sessionID)
            ),
            new StreamedRouteAction<EmptyRequestData>(
                "/client/items",
                async (url, info, sessionID, cancellationToken) => await dataCallbacks.GetTemplateItems(url, info, sessionID)
            ),
            new StreamedRouteAction<EmptyRequestData>(
                "/client/handbook/templates",
                async (url, info, sessionID, cancellationToken) =>
                    await dataCallbacks.GetTemplateHandbook(url, info, sessionID)
            ),
            new StreamedRouteAction<EmptyRequestData>(
                "/client/customization",
                async (url, info, sessionID, cancellationToken) => await dataCallbacks.GetTemplateSuits(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/account/customization",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetTemplateCharacter(url, info, sessionID)
            ),
            new StreamedRouteAction<EmptyRequestData>(
                "/client/hideout/production/recipes",
                async (url, info, sessionID, cancellationToken) => await dataCallbacks.GetHideoutProduction(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/hideout/settings",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetHideoutSettings(url, info, sessionID)
            ),
            new StreamedRouteAction<EmptyRequestData>(
                "/client/hideout/areas",
                async (url, info, sessionID, cancellationToken) => await dataCallbacks.GetHideoutAreas(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/languages",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetLocalesLanguages(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/hideout/qte/list",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetQteList(url, info, sessionID)
            ),
            new StreamedRouteAction<EmptyRequestData>(
                "/client/dialogue",
                async (url, info, sessionID, cancellationToken) => await dataCallbacks.GetDialogue(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/quest/getMainQuestNotesList",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetMainQuestNoteList(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/quest/getMainQuestsList",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetMainQuestsList(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/variable/group",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetVariableGroup(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/subtitle-track/list",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetSubtitleTrackList(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/tape/list",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetTapeList(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/ending/list",
                async (url, info, sessionID, output, cancellationToken) => await dataCallbacks.GetEndingList(url, info, sessionID)
            ),
        ]
    )
{ }
