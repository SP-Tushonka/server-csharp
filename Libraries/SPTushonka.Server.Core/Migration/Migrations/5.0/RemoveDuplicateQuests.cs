using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

/// <summary>
///     Accepting a repeatable quest on earlier 5.0 builds added its entry twice. Keep the first entry of each quest.
/// </summary>
[Injectable]
public sealed class RemoveDuplicateQuests : AbstractProfileMigration
{
    public override string MigrationName
    {
        get { return "RemoveDuplicateQuests500"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        return profile.TryGetArray(out var quests, "characters", "pmc", "Quests") && DuplicateEntries(quests).Count > 0;
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        if (profile.TryGetArray(out var quests, "characters", "pmc", "Quests"))
        {
            foreach (var duplicate in DuplicateEntries(quests))
            {
                quests.Remove(duplicate);
            }
        }

        return base.Migrate(profile);
    }

    private static List<JsonNode> DuplicateEntries(JsonArray quests)
    {
        HashSet<string> seen = [];

        return quests
            .Where(quest => quest is not null && !seen.Add(quest.TryGetValue<string>(out var questId, "qid") ? questId : string.Empty))
            .Select(quest => quest!)
            .ToList();
    }
}
