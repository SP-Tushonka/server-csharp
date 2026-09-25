using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers.Ws;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils;
using ProfileFixerService = SPTarkov.Server.Core.Services.Profile.ProfileFixerService;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class GameController(
    ISptLogger<GameController> logger,
    HideoutTable hideoutTable,
    LocaleTable localeTable,
    BotTable botTable,
    IReadOnlyList<SptMod> loadedMods,
    TimeUtil timeUtil,
    HttpServerHelper httpServerHelper,
    HideoutHelper hideoutHelper,
    HealthHelper healthHelper,
    ProfileHelper profileHelper,
    ProfileFixerService profileFixerService,
    ServerLocalisationService serverLocalisationService,
    PostDbLoadService postDbLoadService,
    SeasonalEventService seasonalEventService,
    ProfileActivityService profileActivityService,
    SaveServer saveServer,
    BotConfig botConfig,
    CoreConfig coreConfig,
    HideoutConfig hideoutConfig,
    HttpConfig httpConfig,
    HealthConfig healthConfig,
    BattlePassDocumentLimitService battlePassDocumentLimitService
) : IOnUpdate
{
    /// <summary>
    ///     Handle client/game/start
    /// </summary>
    /// <param name="url"></param>
    /// <param name="sessionId">Session/Player id</param>
    /// <param name="startTimeStampMs"></param>
    public async Task GameStart(string url, MongoId sessionId, long startTimeStampMs)
    {
        profileActivityService.AddActiveProfile(sessionId, startTimeStampMs);

        if (sessionId.IsEmpty)
        {
            logger.Error($"{nameof(sessionId)} is empty on GameController.GameStart");
            return;
        }

        // repeatableQuests are stored by in profile.Quests due to the responses of the client (e.g. Quests in
        // offraidData). Since we don't want to clutter the Quests list, we need to remove all completed (failed or
        // successful) repeatable quests. We also have to remove the Counters from the repeatableQuests

        var fullProfile = profileHelper.GetFullProfile(sessionId);
        if (fullProfile is null)
        {
            logger.Error($"{nameof(fullProfile)} is null on GameController.GameStart");
            return;
        }

        fullProfile.FriendProfileIds ??= [];

        if (fullProfile.ProfileInfo?.IsWiped is not null && fullProfile.ProfileInfo.IsWiped.Value)
        {
            return;
        }

        if (fullProfile.ProfileInfo?.InvalidOrUnloadableProfile is not null && fullProfile.ProfileInfo.InvalidOrUnloadableProfile.Value)
        {
            return;
        }

        fullProfile.CharacterData!.PmcData!.WishList ??= new();
        fullProfile.CharacterData.ScavData!.WishList ??= new();

        profileFixerService.FixSuitUnlockTypes(fullProfile);

        if (fullProfile.DialogueRecords is not null)
        {
            profileFixerService.CheckForAndFixDialogueAttachments(fullProfile);
        }

        if (logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"Started game with session {sessionId} {fullProfile.ProfileInfo?.Username}");
        }

        var pmcProfile = fullProfile.CharacterData.PmcData;

        if (coreConfig.Fixes.FixProfileBreakingInventoryItemIssues)
        {
            profileFixerService.FixProfileBreakingInventoryItemIssues(pmcProfile);
        }

        if (pmcProfile.Health is not null)
        {
            UpdateProfileHealthValues(pmcProfile);
        }

        profileFixerService.CheckForAndRemoveInvalidTraders(fullProfile);
        profileFixerService.CheckForAndFixPmcProfileIssues(pmcProfile);

        if (pmcProfile.Hideout is not null)
        {
            profileFixerService.AddMissingHideoutBonusesToProfile(pmcProfile, hideoutTable.Areas);
            hideoutHelper.SetHideoutImprovementsToCompleted(pmcProfile);
            pmcProfile.UnlockHideoutWallInProfile();

            // Handle if player has been inactive for a long time, catch up on hideout update before the user goes to his hideout
            if (!profileActivityService.ActiveWithinLastMinutes(sessionId, hideoutConfig.UpdateProfileHideoutWhenActiveWithinMinutes))
            {
                hideoutHelper.UpdatePlayerHideout(sessionId);
            }
        }

        LogProfileDetails(fullProfile);
        SaveActiveModsToProfile(fullProfile);

        if (pmcProfile.Info is not null)
        {
            AddPlayerToPmcNames(pmcProfile);
        }

        if (pmcProfile.Skills?.Common is not null)
        {
            WarnOnActiveBotReloadSkill(pmcProfile);
        }

        await seasonalEventService.GivePlayerSeasonalGiftsAsync(sessionId);

        // Set activity timestamp at the end of the method, so that code that checks for an older timestamp (Updating hideout) can still run
        profileActivityService.SetActivityTimestamp(sessionId);
    }

    /// <summary>
    ///     Handle client/game/config
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns>GameConfigResponse</returns>
    public GameConfigResponse GetGameConfig(MongoId sessionId)
    {
        var profile = profileHelper.GetPmcProfile(sessionId);
        var gameTime =
            profile?.Stats?.Eft?.OverallCounters?.Items?.FirstOrDefault(c => c.Key!.Contains("LifeTime") && c.Key.Contains("Pmc"))?.Value
            ?? 0D;

        var config = new GameConfigResponse
        {
            Languages = localeTable.Languages,
            IsNdaFree = false,
            IsReportAvailable = false,
            IsTwitchEventMember = false,
            Language = "en",
            Aid = profile?.Aid,
            Taxonomy = 6,
            ActiveProfileId = sessionId,
            Backend = new Backend
            {
                Lobby = $"{httpServerHelper.GetWebsocketUrl()}{SessionRequestWebSocketHandler.HookUrl}{sessionId}",
                Trading = httpServerHelper.GetBackendUrl(),
                Messaging = httpServerHelper.GetBackendUrl(),
                Main = httpServerHelper.GetBackendUrl(),
                Static = httpServerHelper.GetBackendUrl(),
                RagFair = httpServerHelper.GetBackendUrl(),
            },
            UseProtobuf = false,
            UtcTime = timeUtil.GetTimeStamp(),
            TotalInGame = gameTime,
            SessionMode = "pve",
            PurchasedGames = new PurchasedGames { IsEftPurchased = true, IsArenaPurchased = false },
            IsGameSynced = true,
            LinkedPlatforms = [],
            AvailableGameModes = new Dictionary<string, bool>
            {
                { "regular", true },
                { "pve", true },
                { "pvp-season", true },
            },
        };

        return config;
    }

    /// <summary>
    ///     Handle client/game/mode
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <param name="requestData"></param>
    /// <returns></returns>
    public GameModeResponse GetGameMode(MongoId sessionId, GameModeRequestData requestData)
    {
        return new GameModeResponse { GameMode = "pve", BackendUrl = httpServerHelper.GetBackendUrl() };
    }

    /// <summary>
    ///     Handle client/server/list
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns></returns>
    public List<ServerDetails> GetServer(MongoId sessionId)
    {
        return [new ServerDetails { Ip = httpServerHelper.GetBackendHost(), Port = httpConfig.BackendPort }];
    }

    /// <summary>
    ///     Handle client/match/group/current
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns></returns>
    public CurrentGroupResponse GetCurrentGroup(MongoId sessionId)
    {
        return new CurrentGroupResponse { Squad = [] };
    }

    /// <summary>
    ///     Handle client/checkVersion
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns></returns>
    public CheckVersionResponse GetValidGameVersion(MongoId sessionId)
    {
        return new CheckVersionResponse { IsValid = true, LatestVersion = coreConfig.CompatibleTarkovVersion };
    }

    /// <summary>
    ///     Handle client/game/keepalive
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns></returns>
    public GameKeepAliveResponse GetKeepAlive(MongoId sessionId)
    {
        profileActivityService.SetActivityTimestamp(sessionId);
        return new GameKeepAliveResponse { Message = "OK", UtcTime = timeUtil.GetTimeStamp() };
    }

    /// <summary>
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns></returns>
    public SurveyResponseData GetSurvey(MongoId sessionId)
    {
        return coreConfig.Survey;
    }

    /// <summary>
    ///     Players set botReload to a high value and don't expect the crazy fast reload speeds, give them a warn about it
    /// </summary>
    /// <param name="pmcProfile">Player profile</param>
    protected void WarnOnActiveBotReloadSkill(PmcData pmcProfile)
    {
        var botReloadSkill = pmcProfile.GetSkillFromProfile(SkillTypes.BotReload);
        if (botReloadSkill?.Progress > 0)
        {
            logger.Warning(serverLocalisationService.GetText("server_start_player_active_botreload_skill"));
        }
    }

    /// <summary>
    ///     Update health values and effect timers of profiles active within the configured minutes
    /// </summary>
    public void UpdateActiveProfilesHealth()
    {
        foreach (var (sessionId, profile) in saveServer.GetProfiles())
        {
            if (saveServer.IsProfileInvalidOrUnloadable(sessionId))
            {
                continue;
            }

            var pmcProfile = profile.CharacterData?.PmcData;
            if (
                pmcProfile?.Health is not null
                && profileActivityService.ActiveWithinLastMinutes(sessionId, healthConfig.UpdateProfileHealthWhenActiveWithinMinutes)
            )
            {
                UpdateProfileHealthValues(pmcProfile);
            }
        }
    }

    /// <summary>
    ///     Iterate over all active effects and reduce timer, runs on login and periodically while active
    /// </summary>
    /// <param name="pmcProfile">Profile to adjust values for</param>
    //Todo: Remove this at some point probably, it's just kept here to keep mods compatible
    protected void UpdateProfileHealthValues(PmcData pmcProfile)
    {
        healthHelper.UpdateProfileHealthValues(pmcProfile, DecreaseBodyPartEffectTimes);
    }

    /// <summary>
    ///     Check for and update any timers on effect found on body parts
    /// </summary>
    /// <param name="pmcProfile">Player</param>
    /// <param name="hpRegenPerHour"></param>
    /// <param name="diffSeconds"></param>
    //Todo: Remove this at some point probably, it's just kept here to keep mods compatible
    protected void DecreaseBodyPartEffectTimes(PmcData pmcProfile, double hpRegenPerHour, double diffSeconds)
    {
        healthHelper.DecreaseBodyPartEffectTimes(pmcProfile, hpRegenPerHour, diffSeconds);
    }

    /// <summary>
    ///     Get a list of installed mods and save their details to the profile being used
    /// </summary>
    /// <param name="fullProfile">Profile to add mod details to</param>
    protected void SaveActiveModsToProfile(SptProfile fullProfile)
    {
        fullProfile.SptData!.Mods ??= [];

        foreach (var mod in loadedMods)
        {
            if (
                fullProfile.SptData.Mods.Any(m =>
                    m.Author == mod.ModMetadata.Author && m.Version == mod.ModMetadata.Version.ToString() && m.Name == mod.ModMetadata.Name
                )
            )
            {
                // exists already, skip
                continue;
            }

            fullProfile.SptData.Mods.Add(
                new ModDetails
                {
                    Author = mod.ModMetadata.Author,
                    Version = mod.ModMetadata.Version.ToString(),
                    Name = mod.ModMetadata.Name,
                    Url = mod.ModMetadata.Url,
                    DateAdded = timeUtil.GetTimeStamp(),
                }
            );
        }
    }

    /// <summary>
    ///     Add the logged in players name to PMC name pool
    /// </summary>
    /// <param name="pmcProfile">Profile of player to get name from</param>
    protected void AddPlayerToPmcNames(PmcData pmcProfile)
    {
        var playerName = pmcProfile.Info?.Nickname;
        if (playerName is not null)
        {
            var bots = botTable.Types;

            // Official names can only be 15 chars in length
            if (playerName.Length > botConfig.BotNameLengthLimit)
            {
                return;
            }

            // Skip if player name exists already
            if (bots!.TryGetValue("bear", out var bearBot))
            {
                if (bearBot is not null && bearBot.FirstNames!.Any(x => x == playerName))
                {
                    bearBot.FirstNames!.Add(playerName);
                }
            }

            if (bots.TryGetValue("bear", out var usecBot))
            {
                if (usecBot is not null && usecBot.FirstNames!.Any(x => x == playerName))
                {
                    usecBot.FirstNames!.Add(playerName);
                }
            }
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="fullProfile"></param>
    protected void LogProfileDetails(SptProfile fullProfile)
    {
        if (logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"Profile made with: {fullProfile.SptData?.Version}");
            logger.Debug($"Server version: {ProgramStatics.SPT_VERSION()} {ProgramStatics.COMMIT()}");
            logger.Debug($"Debug enabled: {ProgramStatics.DEBUG()}");
            logger.Debug($"Mods enabled: {ProgramStatics.MODS()}");
        }
    }

    public void Load()
    {
        postDbLoadService.PerformPostDbLoadActions();
    }

    public Task<bool> OnUpdateAsync(long secondsSinceLastRun, CancellationToken cancellationToken)
    {
        battlePassDocumentLimitService.RefillExpiredBattlePassLimits();

        return Task.FromResult(true);
    }
}
