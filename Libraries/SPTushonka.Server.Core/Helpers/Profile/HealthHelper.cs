using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Exceptions.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using BodyPartHealth = SPTarkov.Server.Core.Models.Eft.Common.Tables.BodyPartHealth;

namespace SPTarkov.Server.Core.Helpers.Profile;

[Injectable]
public class HealthHelper(ISptLogger<HealthHelper> logger, TimeUtil timeUtil, HealthConfig healthConfig)
{
    protected readonly HashSet<string> EffectsToSkip = ["Dehydration", "Exhaustion"];
    private const int LightBleedingTimeInSeconds = 600;
    private const int HeavyBleedingTimeInSeconds = 900;

    /// <summary>
    ///     Update player profile vitality values with changes from client request object
    /// </summary>
    /// <param name="pmcProfileToUpdate">Player profile to apply changes to</param>
    /// <param name="healthChanges">Changes to apply </param>
    /// <param name="isDead">Is the player dead</param>
    public void ApplyHealthChangesToProfile(PmcData pmcProfileToUpdate, BotBaseHealth healthChanges, bool isDead)
    {
        if (healthChanges.BodyParts is null)
        {
            const string message = "healthChanges.BodyParts is null when trying to apply health changes";
            logger.Error(message);
            throw new HealthHelperException(message);
        }

        var playerWasCursed = !PlayerHadGearOnRaidStart(pmcProfileToUpdate.Inventory!);

        // Alter saved profiles Health with values from post-raid client data
        ModifyProfileHealthProperties(pmcProfileToUpdate, healthChanges.BodyParts, EffectsToSkip, isDead, playerWasCursed);

        // Adjust hydration/energy/temperature
        AdjustProfileHydrationEnergyTemperature(pmcProfileToUpdate, healthChanges);

        if (pmcProfileToUpdate.Health is null)
        {
            const string message = "pmcProfileToUpdate.Health is null when trying to apply health changes";
            logger.Error(message);
            throw new HealthHelperException(message);
        }

        // Update last edited timestamp
        pmcProfileToUpdate.Health.UpdateTime = timeUtil.GetTimeStamp();
    }

    /// <summary>
    /// Did the player start raid with gear, if false, they are 'cursed'
    /// </summary>
    /// <param name="inventory">Players inventory at start of raid</param>
    /// <returns>True = they had enough gear to not be classed as 'cursed'</returns>
    protected bool PlayerHadGearOnRaidStart(BotBaseInventory inventory)
    {
        if (inventory.Items == null)
        {
            return false;
        }

        var hasWeapon = false;
        var hasVestRigOrBackpack = false;
        foreach (var item in inventory.Items)
        {
            // Possible early escape
            if (hasWeapon && hasVestRigOrBackpack)
            {
                return true;
            }

            if (item.SlotId is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster")
            {
                hasWeapon = true;
                continue;
            }

            if (item.SlotId is "Backpack" or "ArmorVest" or "TacticalVest")
            {
                hasVestRigOrBackpack = true;
            }
        }

        return hasWeapon && hasVestRigOrBackpack;
    }

    /// <summary>
    ///     Apply Health values to profile
    /// </summary>
    /// <param name="profileToAdjust">Player profile on server</param>
    /// <param name="bodyPartChanges">Changes to apply</param>
    /// <param name="effectsToSkip"></param>
    /// <param name="isDead"></param>
    /// <param name="playerWasCursed">Did player enter raid with no equipment</param>
    protected void ModifyProfileHealthProperties(
        PmcData profileToAdjust,
        Dictionary<string, BodyPartHealth> bodyPartChanges,
        HashSet<string>? effectsToSkip = null,
        bool isDead = false,
        bool playerWasCursed = false
    )
    {
        var debuffEndDelayPercent =
            profileToAdjust
                .Bonuses?.Where(bonus => bonus.Type == BonusType.DebuffEndDelay)
                .Aggregate(0d, (sum, bonus) => sum + bonus.Value!.Value)
            ?? 0d;

        var debuffEndDelayMultiplier = Math.Max(0d, 1d + (debuffEndDelayPercent / 100d));

        foreach (var (partName, partProperties) in bodyPartChanges)
        {
            // Pattern matching null and false because otherwise the compiler throws a fit because `matchingProfilePart`
            // might not be initialized, very cool
            if (profileToAdjust.Health?.BodyParts?.TryGetValue(partName, out var matchingProfilePart) is null or false)
            {
                continue;
            }

            if (partProperties.Health is null || matchingProfilePart.Health is null)
            {
                const string message =
                    "partProperties.Health or matchingBodyPart.Health is null when trying to modify profile health properties";
                logger.Error(message);
                throw new HealthHelperException(message);
            }

            if (healthConfig.Save.Health)
            {
                // Apply hp changes to profile
                if (!isDead)
                {
                    // If the player isn't dead, restore blacked limbs with a penalty
                    matchingProfilePart.Health.Current =
                        partProperties.Health.Current == 0
                            ? matchingProfilePart.Health.Maximum * healthConfig.HealthMultipliers.Blacked
                            : partProperties.Health.Current;
                }
                else
                {
                    // If the player died, set all limbs with a penalty
                    matchingProfilePart.Health.Current = matchingProfilePart.Health.Maximum * healthConfig.HealthMultipliers.Death;

                    // Cursed player, body part gets set to 1 on death
                    if (playerWasCursed)
                    {
                        matchingProfilePart.Health.Current = 1;
                    }
                }
            }

            // Have effects we need to add, init effect array
            matchingProfilePart.Effects ??= [];

            // Anything the server has for this part that the client did NOT report is no longer active (expired or treated in raid) - remove it
            var clientKeys = partProperties.Effects?.Keys ?? Enumerable.Empty<string>();
            foreach (var serverKey in matchingProfilePart.Effects.Keys.ToList())
            {
                if (!clientKeys.Contains(serverKey))
                {
                    matchingProfilePart.Effects.Remove(serverKey);
                }
            }

            // Process each effect for each part
            foreach (var (key, effectDetails) in partProperties.Effects ?? [])
            {
                var isSkipped = effectsToSkip is not null && effectsToSkip.Contains(key);
                var isBleedKey =
                    key.Equals("LightBleeding", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("HeavyBleeding", StringComparison.OrdinalIgnoreCase);
                var bleedOverrideSeconds = GetBleedExitOverrideTime(key, debuffEndDelayMultiplier);

                if (
                    key.Equals("MildMusclePain", StringComparison.OrdinalIgnoreCase)
                    && matchingProfilePart.Effects.ContainsKey("SevereMusclePain")
                )
                {
                    // Edge case - client is trying to add mild pain when server already has severe, don't allow this
                    continue;
                }

                // Bleed reduced to zero or below by DebuffEndDelay bonuses - treat as fully resolved
                if (isBleedKey && bleedOverrideSeconds is null)
                {
                    matchingProfilePart.Effects.Remove(key);
                    continue;
                }

                // Effect on limb already exists in server profile, handle differently
                if (matchingProfilePart.Effects.ContainsKey(key))
                {
                    matchingProfilePart.Effects.TryGetValue(key, out var matchingEffectOnServer);

                    // Edge case - effect already exists at destination, but we don't want to overwrite details e.g. Exhaustion
                    if (isSkipped)
                    {
                        matchingProfilePart.Effects[key] = null;
                    }
                    else if (bleedOverrideSeconds is not null && matchingEffectOnServer is not null)
                    {
                        // Bleeds always get a fresh timer on raid exit (client always sends -1 for these in-raid)
                        matchingEffectOnServer.Time = bleedOverrideSeconds;
                    }
                    else if (
                        effectDetails?.Time is not null
                        && matchingEffectOnServer?.Time is not null
                        && effectDetails.Time < matchingEffectOnServer.Time
                    )
                    {
                        // Effect time has decreased while in raid, persist this reduction into profile
                        matchingEffectOnServer.Time = effectDetails.Time;
                    }

                    continue;
                }

                if (isSkipped)
                // Do not pass skipped effect into profile
                {
                    continue;
                }

                var effectToAdd = new BodyPartEffectProperties { Time = bleedOverrideSeconds ?? effectDetails?.Time ?? -1 };

                // Add effect to server profile
                if (matchingProfilePart.Effects.TryAdd(key, effectToAdd))
                {
                    matchingProfilePart.Effects[key] = effectToAdd;
                }
            }
        }
    }

    private double? GetBleedExitOverrideTime(string effectKey, double debuffEndDelayMultiplier)
    {
        int baseSeconds;

        if (effectKey.Equals("LightBleeding", StringComparison.OrdinalIgnoreCase))
        {
            baseSeconds = LightBleedingTimeInSeconds;
        }
        else if (effectKey.Equals("HeavyBleeding", StringComparison.OrdinalIgnoreCase))
        {
            baseSeconds = HeavyBleedingTimeInSeconds;
        }
        else
        {
            return null;
        }

        var adjusted = baseSeconds * debuffEndDelayMultiplier;

        // guard in case they have bonuses reducing it beyond 100%
        return adjusted > 0 ? adjusted : null;
    }

    /// <summary>
    ///     Adjust hydration/energy/temperate
    /// </summary>
    /// <param name="profileToUpdate">Profile to update</param>
    /// <param name="healthChanges"></param>
    protected void AdjustProfileHydrationEnergyTemperature(PmcData profileToUpdate, BotBaseHealth healthChanges)
    {
        // Ensure current hydration/energy/temp are copied over and don't exceed maximum
        var profileHealth = profileToUpdate.Health;
        profileHealth!.Hydration!.Current =
            profileHealth.Hydration.Current > healthChanges.Hydration!.Maximum
                ? healthChanges.Hydration.Maximum
                : Math.Round(healthChanges.Hydration.Current ?? 0);

        profileHealth.Energy!.Current =
            profileHealth.Energy.Current > healthChanges.Energy!.Maximum
                ? healthChanges.Energy.Maximum
                : Math.Round(healthChanges.Energy.Current ?? 0);

        profileHealth.Temperature!.Current =
            profileHealth.Temperature.Current > healthChanges.Temperature!.Maximum
                ? healthChanges.Temperature.Maximum
                : Math.Round(healthChanges.Temperature.Current ?? 0);
    }
}
