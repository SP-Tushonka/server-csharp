using System.Text;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Models.Eft.Seasons;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable(TypePriority = OnLoadOrder.GameCallbacks)]
public class GameCallbacks(
    SessionTokenService sessionTokenService,
    HttpResponseUtil httpResponseUtil,
    Watermark watermark,
    SaveServer saveServer,
    BackupService backupService,
    GameController gameController,
    ProfileActivityService profileActivityService,
    TimeUtil timeUtil,
    SeasonTable seasonTable,
    TarcoinStoreService tarcoinStoreService,
    HttpServerHelper httpServerHelper,
    ISptLogger<GameCallbacks> logger
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        gameController.Load();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handle client/game/version/validate
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> VersionValidate(string url, VersionValidateRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }

    /// <summary>
    ///     Handle client/game/start
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GameStart(string url, EmptyRequestData _, MongoId sessionID)
    {
        if (saveServer.IsProfileInvalidOrUnloadable(sessionID))
        {
            return new ValueTask<string>(
                httpResponseUtil.GetBody(
                    new GameStartResponse { UtcTime = 0 },
                    Models.Enums.BackendErrorCodes.PlayerProfileNotFound,
                    "This profile cannot be loaded due to it being invalid or unloadable!"
                )
            );
        }

        var startTimestampSec = timeUtil.GetTimeStamp();
        gameController.GameStart(url, sessionID, startTimestampSec);
        return new ValueTask<string>(httpResponseUtil.GetBody(new GameStartResponse { UtcTime = startTimestampSec }));
    }

    /// <summary>
    ///     Handle client/game/logout
    ///     Save profiles on game close
    /// </summary>
    /// <returns></returns>
    public async ValueTask<string> GameLogout(string url, EmptyRequestData _, MongoId sessionID)
    {
        await saveServer.SaveProfileAsync(sessionID);

        // Backup profiles on exit
        await backupService.InitializeAsync();

        return httpResponseUtil.GetBody(new GameLogoutResponseData { Status = "ok" });
    }

    /// <summary>
    ///     Handle client/game/config
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetGameConfig(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(gameController.GetGameConfig(sessionID)));
    }

    /// <summary>
    ///     Handle client/putHWMetrics
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> PutHwMetrics(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody<string>(null!));
    }

    /// <summary>
    ///     Handle client/season/active
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetActiveSeason(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(seasonTable.Active));
    }

    /// <summary>
    ///     Handle /v2/client/shop/status
    /// </summary>
    public ValueTask<string> GetShopStatus(string url, EmptyRequestData info, MongoId sessionID)
    {
        var profile = saveServer.GetProfile(sessionID);
        var shopStatusBody = httpResponseUtil.NoBody(
            new ShopData<ShopStatusResponse>
            {
                Data = new ShopStatusResponse
                {
                    Aid = profile?.ProfileInfo?.Aid,
                    Labels = [],
                    Tarcoins = tarcoinStoreService.GetBalance(sessionID),
                },
            }
        );

        return new ValueTask<string>(shopStatusBody);
    }

    /// <summary>
    ///     Handle /v2/shop/api/v1/account/balance/
    /// </summary>
    public ValueTask<string> GetShopBalance(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(tarcoinStoreService.GetBalanceResponse(sessionID)));
    }

    /// <summary>
    ///     Handle /v2/shop/api/v1/menu
    /// </summary>
    public ValueTask<string> GetShopMenu(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(tarcoinStoreService.GetMenu()));
    }

    /// <summary>
    ///     Handle /v2/shop/api/v1/catalog/{offerId}
    /// </summary>
    public ValueTask<string> GetShopCatalogItem(string url, EmptyRequestData info, MongoId sessionID)
    {
        var offerId = url.Split('?')[0].TrimEnd('/').Split('/').Last();

        return new ValueTask<string>(httpResponseUtil.NoBody(tarcoinStoreService.GetOffer(offerId)));
    }

    /// <summary>
    ///     Handle /v2/shop/api/v1/purchase/single
    /// </summary>
    public async ValueTask<string> PurchaseShopOffer(string url, ShopPurchaseRequest info, MongoId sessionID)
    {
        var bought = await tarcoinStoreService.TryPurchaseAsync(sessionID, info.OfferId ?? string.Empty, info.Count ?? 1);

        return httpResponseUtil.NoBody(new ShopData<ShopPurchaseResult> { Data = new ShopPurchaseResult { Success = bought } });
    }

    /// <summary>
    ///     Handle /client/game/token/issue
    /// </summary>
    public ValueTask<string> IssueGameToken(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(new GameTokenResponse { Token = sessionTokenService.IssueToken(sessionID) }));
    }

    /// <summary>
    ///     Handle /v2/client/shop/token/generate
    /// </summary>
    public ValueTask<string> GenerateShopToken(string url, EmptyRequestData info, MongoId sessionID)
    {
        var id = sessionID.ToString();

        return new ValueTask<string>(
            httpResponseUtil.GetBody(
                new ExpansionsAccessData
                {
                    Id = id,
                    Success = true,
                    Token = id,
                }
            )
        );
    }

    /// <summary>
    ///     Handle /v2/client/shop/purchase/sign
    /// </summary>
    public ValueTask<string> SignShopPurchase(string url, EmptyRequestData info, MongoId sessionID)
    {
        //Todo: Implement!
        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }

    /// <summary>
    ///     Handle client/battle-pass/active
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetActiveBattlePass(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(seasonTable.BattlePass));
    }

    /// <summary>
    ///     Handle client/seasonal-perks/list
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetSeasonalPerks(string url, EmptyRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(seasonTable.Perks));
    }

    /// <summary>
    ///     Handle client/game/mode
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetGameMode(string url, GameModeRequestData info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(gameController.GetGameMode(sessionID, info)));
    }

    /// <summary>
    ///     Handle client/server/list
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetServer(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(gameController.GetServer(sessionID)));
    }

    /// <summary>
    ///     Handle client/match/group/current
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetCurrentGroup(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(gameController.GetCurrentGroup(sessionID)));
    }

    /// <summary>
    ///     Handle client/checkVersion
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> ValidateGameVersion(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(gameController.GetValidGameVersion(sessionID)));
    }

    /// <summary>
    ///     Handle client/game/keepalive
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GameKeepalive(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(gameController.GetKeepAlive(sessionID)));
    }

    /// <summary>
    ///     Handle singleplayer/settings/version
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetVersion(string url, EmptyRequestData _, MongoId sessionID)
    {
        // change to be a proper type
        return new ValueTask<string>(httpResponseUtil.NoBody(new { Version = watermark.GetInGameVersionLabel() }));
    }

    /// <summary>
    ///     Handle /client/report/send and handle /client/reports/lobby/send
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> ReportNickname(string url, UIDRequestData request, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }

    /// <summary>
    ///     Handle singleplayer/settings/getRaidTime
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetRaidTime(string url, GetRaidTimeRequest request, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(gameController.GetRaidTime(sessionID, request)));
    }

    /// <summary>
    ///     Handle /client/survey
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetSurvey(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(gameController.GetSurvey(sessionID)));
    }

    /// <summary>
    ///     Handle client/survey/view
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetSurveyView(string url, SendSurveyOpinionRequest request, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }

    /// <summary>
    ///     Handle client/survey/opinion
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> SendSurveyOpinion(string url, SendSurveyOpinionRequest request, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }

    /// <summary>
    ///     Handle singleplayer/clientmods
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> ReceiveClientMods(string url, SendClientModsRequest request, MongoId sessionID)
    {
        profileActivityService.SetProfileActiveClientMods(sessionID, request.ActiveClientMods);

        return new ValueTask<string>(httpResponseUtil.NullResponse());
    }
}
