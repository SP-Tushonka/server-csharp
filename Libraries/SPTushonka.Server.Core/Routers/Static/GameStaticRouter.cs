using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Routers.Static;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class GameStaticRouter(JsonUtil jsonUtil, GameCallbacks gameCallbacks)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/client/game/config",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetGameConfig(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/putHWMetrics",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.PutHwMetrics(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/season/active",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetActiveSeason(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/game/token/issue",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.IssueGameToken(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/v2/client/shop/status",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetShopStatus(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/v2/client/shop/token/generate",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GenerateShopToken(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/v2/client/shop/purchase/sign",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.SignShopPurchase(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/battle-pass/active",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetActiveBattlePass(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/seasonal-perks/list",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetSeasonalPerks(url, info, sessionID)
            ),
            new RouteAction<GameModeRequestData>(
                "/client/game/mode",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetGameMode(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/server/list",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetServer(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/match/group/current",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetCurrentGroup(url, info, sessionID)
            ),
            new RouteAction<VersionValidateRequestData>(
                "/client/game/version/validate",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.VersionValidate(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/game/start",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GameStart(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/game/logout",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GameLogout(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/checkVersion",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.ValidateGameVersion(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/game/keepalive",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GameKeepalive(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/singleplayer/settings/version",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetVersion(url, info, sessionID)
            ),
            new RouteAction<UIDRequestData>(
                "/client/reports/lobby/send",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.ReportNickname(url, info, sessionID)
            ),
            new RouteAction<UIDRequestData>(
                "/client/report/send",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.ReportNickname(url, info, sessionID)
            ),
            new RouteAction<GetRaidTimeRequest>(
                "/singleplayer/settings/getRaidTime",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetRaidTime(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/client/survey",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetSurvey(url, info, sessionID)
            ),
            new RouteAction<SendSurveyOpinionRequest>(
                "/client/survey/view",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetSurveyView(url, info, sessionID)
            ),
            new RouteAction<SendSurveyOpinionRequest>(
                "/client/survey/opinion",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.SendSurveyOpinion(url, info, sessionID)
            ),
            new RouteAction<SendClientModsRequest>(
                "/singleplayer/clientmods",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.ReceiveClientMods(url, info, sessionID)
            ),
        ]
    )
{ }
