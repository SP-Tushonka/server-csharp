using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Migration.Migrations._4._1;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

/// <summary>
///     1.2 gave every prestige level a new id. The client counts a level as claimed only when the profile holds its
///     current id, so a 4.1 prestige shows as unclaimed until it is renamed to the level it stood for.
/// </summary>
[Injectable]
public sealed class RekeyPrestigeLevels : AbstractProfileMigration
{
    /// <summary>
    ///     4.1 prestige id and the 1.2 id of the same level
    /// </summary>
    private static readonly Dictionary<string, string> NewPrestigeIds = new()
    {
        { "672df12f97f0469cea52f55e", "6aad4b9ebe4ab598ffc34517" },
        { "672df4281ab8d9c8849a0c88", "6aad4b9ef51244f6fc01b426" },
        { "683da91d6f472cfa738c52f2", "6aad4b9e585f726d1e35dd9f" },
        { "6842f121000d98ce33b9a60f", "6aad4b9e8f51b29d5e0bb0b2" },
    };

    public override string MigrationName
    {
        get { return "RekeyPrestigeLevels500"; }
    }

    // The achievement migration still looks prestiges up by their 4.1 ids
    public override IEnumerable<Type> PrerequisiteMigrations
    {
        get { return [typeof(AddMissingPrestigesToProfile)]; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        return profile.TryGetObject(out var prestiges, "characters", "pmc", "Prestige")
            && prestiges.Any(p => NewPrestigeIds.ContainsKey(p.Key));
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        if (profile.TryGetObject(out var prestiges, "characters", "pmc", "Prestige"))
        {
            foreach (var (oldId, newId) in NewPrestigeIds)
            {
                if (!prestiges.Remove(oldId, out var claimedAt))
                {
                    continue;
                }

                prestiges.TryAdd(newId, claimedAt);
            }
        }

        return base.Migrate(profile);
    }
}
