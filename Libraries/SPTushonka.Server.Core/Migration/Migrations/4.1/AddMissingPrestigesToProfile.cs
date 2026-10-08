using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Migration.Migrations._4._1;

[Injectable]
public sealed class AddMissingPrestigesToProfile : AbstractProfileMigration
{
    /// <summary>
    ///     Achievement of each prestige id 4.1 profiles store
    /// </summary>
    private static readonly Dictionary<string, MongoId> PrestigeAchievements = new()
    {
        { "672df12f97f0469cea52f55e", new MongoId("676091c0f457869a94017a23") },
        { "672df4281ab8d9c8849a0c88", new MongoId("676094451fec2f7426093be6") },
        { "683da91d6f472cfa738c52f2", new MongoId("6842c25bd02bc07d70054019") },
        { "6842f121000d98ce33b9a60f", new MongoId("6842c27a38482d35ac0bd847") },
    };

    public override string MigrationName
    {
        get { return "AddMissingPrestigesToProfile"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        if (
            profile.TryGetObject(out var prestiges, "characters", "pmc", "Prestige")
            && profile.TryGetObject(out var achievements, "characters", "pmc", "Achievements")
        )
        {
            foreach (var prestige in prestiges)
            {
                var id = prestige.Key;

                if (PrestigeAchievements.TryGetValue(id, out var AchievementId))
                {
                    if (!achievements.ContainsKey(AchievementId))
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
            profile.TryGetObject(out var prestiges, "characters", "pmc", "Prestige")
            && profile.TryGetObject(out var achievements, "characters", "pmc", "Achievements")
        )
        {
            foreach (var prestige in prestiges)
            {
                var id = prestige.Key;
                var timestamp = prestige.Value!.GetValue<long>();

                if (PrestigeAchievements.TryGetValue(id, out var AchievementId))
                {
                    if (!achievements.ContainsKey(AchievementId))
                    {
                        achievements.Add(AchievementId, timestamp);
                    }
                }
            }
        }

        return base.Migrate(profile);
    }
}
