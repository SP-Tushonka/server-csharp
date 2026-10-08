using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

/// <summary>
///     Prestige extra daily and weekly quests were stored as 0 on their first grant, so no profile ever got them.
///     Prestige is the only source of these entries, so a 0 is a lost grant and is rebuilt from the claimed levels.
/// </summary>
[Injectable]
public sealed class RestorePrestigeExtraRepeatables(TemplateTable templateTable) : AbstractProfileMigration
{
    public override string MigrationName
    {
        get { return "RestorePrestigeExtraRepeatables500"; }
    }

    // Claimed levels are matched by their 1.2 ids
    public override IEnumerable<Type> PrerequisiteMigrations
    {
        get { return [typeof(RekeyPrestigeLevels)]; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        return profile.TryGetObject(out var extras, "spt", "extraRepeatableQuests")
            && extras.Any(extra => (extra.Value?.GetValue<double>() ?? 0) == 0);
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        if (!profile.TryGetObject(out var extras, "spt", "extraRepeatableQuests"))
        {
            return base.Migrate(profile);
        }

        profile.TryGetObject(out var claimed, "characters", "pmc", "Prestige");
        var granted = (templateTable.Prestige.Elements ?? [])
            .Where(level => claimed?.ContainsKey(level.Id) ?? false)
            .SelectMany(level => level.Rewards)
            .Where(reward => reward.Type == RewardType.ExtraDailyQuest)
            .GroupBy(reward => reward.Target)
            .ToDictionary(rewards => rewards.Key!, rewards => rewards.Sum(reward => reward.Value ?? 0));

        foreach (var (repeatableId, value) in extras.ToList())
        {
            if ((value?.GetValue<double>() ?? 0) != 0)
            {
                continue;
            }

            // An entry no claimed level grants is dropped, so the migration does not apply again
            if (granted.TryGetValue(repeatableId, out var count) && count > 0)
            {
                extras[repeatableId] = count;
            }
            else
            {
                extras.Remove(repeatableId);
            }
        }

        return base.Migrate(profile);
    }
}
