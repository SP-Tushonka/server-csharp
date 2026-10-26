using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;

namespace SPTarkov.Server.Core.Migration.Migrations.Fixes;

[Injectable]
public sealed class AddMissingPrestigesToProfile : AbstractProfileMigration
{
    public override string FromVersion
    {
        get { return "~4.0"; }
    }

    public override string ToVersion
    {
        get { return "~4.0"; }
    }

    public override string MigrationName
    {
        get { return "AddMissingPrestigesToProfile"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        if (
            profile["characters"]?["pmc"]?["Prestige"] is JsonObject prestiges
            && profile["characters"]?["pmc"]?["Achievements"] is JsonObject achievements
        )
        {
            foreach (var prestige in prestiges)
            {
                if (PrestigeHelper.PrestigeAchievements.TryGetValue(prestige.Key, out var achievementId))
                {
                    if (!achievements.ContainsKey(achievementId))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        if (
            profile["characters"]?["pmc"]?["Prestige"] is JsonObject prestiges
            && profile["characters"]?["pmc"]?["Achievements"] is JsonObject achievements
        )
        {
            foreach (var prestige in prestiges)
            {
                if (PrestigeHelper.PrestigeAchievements.TryGetValue(prestige.Key, out var achievementId))
                {
                    if (!achievements.ContainsKey(achievementId))
                    {
                        achievements.Add(achievementId, prestige.Value!.GetValue<long>());
                    }
                }
            }
        }

        return base.Migrate(profile);
    }
}
