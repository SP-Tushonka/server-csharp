using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

[Injectable]
public sealed class UnlockQuestLocations(QuestConfig questConfig, JsonUtil jsonUtil) : AbstractProfileMigration
{
    public override string MigrationName
    {
        get { return "UnlockQuestLocations500"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        return MissingLocations(profile).Count > 0;
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        var missing = MissingLocations(profile);
        if (missing.Count > 0 && profile.TryGetObject(out var pmc, "characters", "pmc"))
        {
            if (!pmc.TryGetArray(out var unlocked, "UnlockedLocations"))
            {
                unlocked = [];
                pmc["UnlockedLocations"] = unlocked;
            }

            foreach (var location in missing)
            {
                unlocked.Add(location);
            }
        }

        return base.Migrate(profile);
    }

    private List<string> MissingLocations(JsonObject profile)
    {
        if (!profile.TryGetObject(out var pmc, "characters", "pmc") || !pmc.TryGetArray(out var quests, "Quests"))
        {
            return [];
        }

        var unlocked = pmc.TryGetArray(out var locations, "UnlockedLocations")
            ? locations.Select(location => location?.GetValue<string>()).ToHashSet()
            : [];

        return (jsonUtil.Deserialize<List<QuestStatus>>(quests.ToJsonString()) ?? [])
            .Where(quest => quest.Status == QuestStatusEnum.Success && questConfig.QuestLocationUnlocks.ContainsKey(quest.QId))
            .Select(quest => questConfig.QuestLocationUnlocks[quest.QId])
            .Where(location => !unlocked.Contains(location))
            .Distinct()
            .ToList();
    }
}
