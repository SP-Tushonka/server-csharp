using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Helpers.Profile;

[Injectable]
public class PrestigeHelper(
    ISptLogger<PrestigeHelper> logger,
    TemplateTable templateTable,
    TimeUtil timeUtil,
    MailSendService mailSendService,
    ProfileHelper profileHelper,
    RewardHelper rewardHelper
)
{
    /// <summary>
    /// The key is prestige id, the value is Achievement id
    /// </summary>
    public static Dictionary<MongoId, MongoId> PrestigeAchievements { get; } =
        new Dictionary<MongoId, MongoId>
        {
            { new MongoId("672df12f97f0469cea52f55e"), new MongoId("676091c0f457869a94017a23") },
            { new MongoId("672df4281ab8d9c8849a0c88"), new MongoId("676094451fec2f7426093be6") },
            { new MongoId("683da91d6f472cfa738c52f2"), new MongoId("6842c25bd02bc07d70054019") },
            { new MongoId("6842f121000d98ce33b9a60f"), new MongoId("6842c27a38482d35ac0bd847") },
        };

    public void ProcessPendingPrestige(SptProfile oldProfile, SptProfile newProfile, PendingPrestige prestige)
    {
        var prePrestigePmc = oldProfile.CharacterData?.PmcData;
        var sessionId = newProfile.ProfileInfo?.ProfileId;
        var prestigeLevels = templateTable.Prestige.Elements ?? [];
        var indexOfPrestigeObtained = Math.Clamp((prestige.PrestigeLevel ?? 1) - 1, 0, prestigeLevels.Count - 1); // Levels are 1 to 4, Index is 0 to 3

        var prestigeLevel = prestigeLevels[indexOfPrestigeObtained];

        // Skill copy
        var skillProgressCopyAmount = (float)(1 - prestigeLevel.TransferConfigs.SkillConfig.TransferMultiplier);
        var masteringProgressCopyAmount = (float)(1 - prestigeLevel.TransferConfigs.MasteringConfig.TransferMultiplier);

        if (prePrestigePmc?.Skills?.Common is not null)
        {
            var commonSKillsToCopy = prePrestigePmc.Skills.Common;
            foreach (var skillToCopy in commonSKillsToCopy)
            {
                // Set progress for 5% of what it was, multiplied by prestige level
                skillToCopy.Progress *= skillProgressCopyAmount;
                var existingSkill = newProfile.CharacterData?.PmcData?.Skills?.Common.FirstOrDefault(skill => skill.Id == skillToCopy.Id);
                if (existingSkill is not null)
                {
                    existingSkill.Progress = skillToCopy.Progress;
                }
                else
                {
                    newProfile.CharacterData!.PmcData!.Skills!.Common = newProfile.CharacterData.PmcData.Skills.Common.Union([skillToCopy]);
                }
            }

            var masteringSkillsToCopy = prePrestigePmc.Skills.Mastering;
            foreach (var skillToCopy in masteringSkillsToCopy ?? [])
            {
                // Set progress 5% of what it was, multiplied by prestige level
                skillToCopy.Progress *= masteringProgressCopyAmount;
                var existingSkill = newProfile.CharacterData?.PmcData?.Skills?.Mastering?.FirstOrDefault(skill =>
                    skill.Id == skillToCopy.Id
                );
                if (existingSkill is not null)
                {
                    existingSkill.Progress = skillToCopy.Progress;
                }
                else
                {
                    newProfile.CharacterData!.PmcData!.Skills!.Mastering = newProfile.CharacterData.PmcData.Skills.Mastering?.Union([
                        skillToCopy,
                    ]);
                }
            }
        }

        var obtainedLevels = prestigeLevels.Slice(0, indexOfPrestigeObtained + 1);
        var achievements = newProfile.CharacterData?.PmcData?.Achievements;
        foreach (var obtainedLevel in obtainedLevels)
        {
            if (!PrestigeAchievements.TryGetValue(obtainedLevel.Id, out var achievement))
            {
                logger.Error($"Unable to add prestige achievement to profile, no prestige achievement for {obtainedLevel.Id} was found.");
            }
            else if (achievements is not null && !achievements.ContainsKey(achievement))
            {
                rewardHelper.AddAchievementToProfile(newProfile, achievement);
            }
        }

        // Get all prestige rewards from prestige 1 up to desired prestige
        var prestigeRewards = obtainedLevels.SelectMany(prestigeInner => prestigeInner.Rewards);

        AddPrestigeRewardsToProfile(sessionId!.Value, newProfile, prestigeRewards);

        // Copy profile stats
        CopyStats(newProfile, oldProfile);

        // Flag profile as having achieved this prestige level and any it skipped
        var prestigeFlags = newProfile.CharacterData?.PmcData?.Prestige;
        foreach (var obtainedLevel in obtainedLevels)
        {
            prestigeFlags?.TryAdd(obtainedLevel.Id, timeUtil.GetTimeStamp());
        }

        if (prestigeFlags?.ContainsKey(prestigeLevel.Id) is false)
        {
            logger.Error(
                $"Failed to add prestige element with id: {prestigeLevel.Id} to new profile during processing of pending prestige."
            );
        }

        var itemsToTransfer = new List<Item>();

        // Copy transferred items
        foreach (var transferRequest in prestige.Items ?? [])
        {
            // Get item with its attached children (if it has any)
            var itemWithChildren = prePrestigePmc?.Inventory?.Items?.GetItemWithChildren(transferRequest.Id);
            if (itemWithChildren is null)
            {
                logger.Error($"Unable to find item with id: {transferRequest.Id} in profile: {sessionId}, skipping");
                continue;
            }

            // Get root item so we can check its pin/lock state
            var rootItem = itemWithChildren.FirstOrDefault(item => item.Id == transferRequest.Id);
            if (rootItem.Upd?.PinLockState is PinLockState.Pinned or PinLockState.Locked)
            {
                // Item has been pinned/locked and needs to be unpinned before sending to player, otherwise they can't transfer item into stash
                rootItem.Upd.PinLockState = PinLockState.Free;
            }

            itemsToTransfer.AddRange(itemWithChildren);
        }

        if (itemsToTransfer.Count > 0)
        {
            mailSendService.SendSystemMessageToPlayer(
                sessionId.Value,
                string.Empty,
                itemsToTransfer,
                timeUtil.GetHoursAsSeconds(8760) // Year
            );
        }

        // Copy over existing unlocked hideout customisation unlocks to new profile that player doesn't already have
        newProfile.CustomisationUnlocks ??= [];
        var newProfileUnlocks = newProfile.CustomisationUnlocks.ToDictionary(unlock => unlock.Id, unlock => unlock);
        foreach (var oldUnlock in oldProfile.CustomisationUnlocks ?? [])
        {
            if (newProfileUnlocks.ContainsKey(oldUnlock.Id))
            {
                continue;
            }

            newProfile.CustomisationUnlocks.Add(oldUnlock);
        }

        // Set prestige level on new profile
        newProfile.CharacterData!.PmcData!.Info!.PrestigeLevel = prestige.PrestigeLevel;
    }

    /// <summary>
    /// Copy over profile stats from old profile to new
    /// Remove some stats once copied over
    /// </summary>
    /// <param name="newProfile">Profile to add stats to</param>
    /// <param name="oldProfile">Profile to copy stats from</param>
    protected void CopyStats(SptProfile newProfile, SptProfile oldProfile)
    {
        var newPmcStats = newProfile.CharacterData.PmcData.Stats;
        var oldPmcStats = oldProfile.CharacterData.PmcData.Stats;

        newPmcStats.Eft = oldPmcStats.Eft;

        // Reset some PMC stats that should not be copied over
        newPmcStats.Eft.CarriedQuestItems = [];
        newPmcStats.Eft.FoundInRaidItems = [];
        newPmcStats.Eft.LastSessionDate = 0;
        newPmcStats.Eft.DroppedItems = [];

        // TODO: find evidence scav stats are copied over in live
        //var newScavStats = newProfile.CharacterData.ScavData.Stats;
        //var oldScavStats = oldProfile.CharacterData.ScavData.Stats;

        //newPmcStats.Eft = oldScavStats.Eft;

        //// Reset some Scav stats that should not be copied over
        //newScavStats.Eft.CarriedQuestItems = [];
        //newScavStats.Eft.FoundInRaidItems = [];
        //newScavStats.Eft.LastSessionDate = 0;
    }

    /// <summary>
    ///     Grant prestige rewards to a profile outside of a prestige wipe, items arrive by mail.
    ///     Goes through AddPrestigeRewardsToProfile so patches on it still apply
    /// </summary>
    /// <param name="sessionId">Profile id</param>
    /// <param name="profile">Profile to reward</param>
    /// <param name="rewards">Rewards of the prestige tiers being granted</param>
    [Obsolete("Remove this on 5.0 by making AddPrestigeRewardsToProfile public")]
    public void GrantPrestigeRewards(MongoId sessionId, SptProfile profile, IEnumerable<Reward> rewards)
    {
        AddPrestigeRewardsToProfile(sessionId, profile, rewards);
    }

    /// <summary>
    ///     Grant the rewards of every prestige level a freshly wiped profile already holds
    /// </summary>
    /// <param name="profile">Newly created profile carrying over its prestige level</param>
    public void GrantHeldPrestigeRewards(SptProfile profile)
    {
        var prestigeLevel = profile.CharacterData?.PmcData?.Info?.PrestigeLevel ?? 0;
        if (prestigeLevel <= 0)
        {
            return;
        }

        var rewards = (templateTable.Prestige.Elements ?? []).Take(prestigeLevel).SelectMany(prestige => prestige.Rewards ?? []);
        AddPrestigeRewardsToProfile(profile.ProfileInfo!.ProfileId!.Value, profile, rewards);
    }

    private void AddPrestigeRewardsToProfile(MongoId sessionId, SptProfile newProfile, IEnumerable<Reward> rewards)
    {
        var itemsToSend = new List<Item>();

        foreach (var reward in rewards)
        {
            switch (reward.Type)
            {
                case RewardType.CustomizationDirect:
                {
                    profileHelper.AddHideoutCustomisationUnlock(newProfile, reward, CustomisationSource.PRESTIGE);
                    break;
                }
                case RewardType.Skill:
                    if (Enum.TryParse(reward.Target, out SkillTypes result))
                    {
                        // skill reward values are always 100 (+1 level), so adjustment for low levels will give a wrong result
                        profileHelper.AddSkillPointsToPlayer(
                            newProfile.CharacterData!.PmcData!,
                            result,
                            reward.Value.GetValueOrDefault(0),
                            useSkillProgressRateMultiplier: false,
                            adjustSkillExpForLowLevels: false
                        );
                    }
                    else
                    {
                        logger.Error($"Unable to parse reward Target to Enum: {reward.Target}");
                    }

                    break;
                case RewardType.Item:
                {
                    itemsToSend.AddRange(reward.Items ?? []);
                    break;
                }
                case RewardType.ExtraDailyQuest:
                {
                    newProfile.AddExtraRepeatableQuest(new MongoId(reward.Target), (double)reward.Value!);
                    break;
                }
                default:
                    logger.Error($"Unhandled prestige reward type: {reward.Type} Id: {reward.Id}");
                    break;
            }
        }

        if (itemsToSend.Count > 0)
        {
            mailSendService.SendSystemMessageToPlayer(sessionId, string.Empty, itemsToSend, 31536000);
        }
    }
}
