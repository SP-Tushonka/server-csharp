using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

/// <summary>
///     Story chapters that started on their own on earlier 5.0 builds skipped their Started rewards, which left the story
///     variable groups short. Set the variables those rewards give.
/// </summary>
[Injectable]
public sealed class AddMissingStartVariables(TemplateTable templateTable) : AbstractProfileMigration
{
    public override string MigrationName
    {
        get { return "AddMissingStartVariables500"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        return MissingVariables(profile).Count > 0;
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        var missing = MissingVariables(profile);
        if (missing.Count > 0 && profile.TryGetObject(out var pmc, "characters", "pmc"))
        {
            if (pmc["Variables"] is not JsonObject variables)
            {
                variables = new JsonObject();
                pmc["Variables"] = variables;
            }

            foreach (var (variableId, value) in missing)
            {
                variables[variableId] = value;
            }
        }

        return base.Migrate(profile);
    }

    /// <summary>
    ///     Get the start variables of started quests that the profile does not have
    /// </summary>
    /// <param name="profile">Profile json</param>
    /// <returns>Variable values by variable id</returns>
    private Dictionary<string, int> MissingVariables(JsonObject profile)
    {
        Dictionary<string, int> missing = [];
        if (!profile.TryGetObject(out var pmc, "characters", "pmc") || !pmc.TryGetArray(out var quests, "Quests"))
        {
            return missing;
        }

        var variables = pmc["Variables"] as JsonObject;
        foreach (var quest in quests)
        {
            // A failed quest or one waiting on its unlock timer has no start variable on live either
            if (
                !quest.TryGetValue<int>(out var status, "status")
                || status is not ((int)QuestStatusEnum.Started or (int)QuestStatusEnum.AvailableForFinish or (int)QuestStatusEnum.Success)
                || !quest.TryGetValue<string>(out var questId, "qid")
            )
            {
                continue;
            }

            var rewards = templateTable.Quests.GetValueOrDefault(new MongoId(questId))?.Rewards?.GetValueOrDefault("Started") ?? [];
            foreach (var reward in rewards.Where(reward => reward.Type == RewardType.GlobalVariable && reward.Target is not null))
            {
                if (!(variables?.ContainsKey(reward.Target!) ?? false))
                {
                    missing.TryAdd(reward.Target!, Convert.ToInt32(reward.Value));
                }
            }
        }

        return missing;
    }
}
