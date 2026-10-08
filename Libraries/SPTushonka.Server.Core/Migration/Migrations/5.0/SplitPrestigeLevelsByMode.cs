using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

/// <summary>
///     1.2 keeps a prestige level per game mode. Every level this server granted was earned in PvE, so it moves to the
///     pve counter and the regular one, the PvP level, goes back to 0. Profiles saved before this held one level, or the
///     same level mirrored into both counters.
/// </summary>
[Injectable]
public sealed class SplitPrestigeLevelsByMode : AbstractProfileMigration
{
    public override string MigrationName
    {
        get { return "SplitPrestigeLevelsByMode500"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        if (!profile.TryGetObject(out var info, "characters", "pmc", "Info"))
        {
            return false;
        }

        if (info["PrestigeLevels"] is JsonObject levels)
        {
            return (levels[PrestigeGameModes.Regular]?.GetValue<int>() ?? 0) > 0;
        }

        return (info["PrestigeLevel"]?.GetValue<int>() ?? 0) > 0;
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        if (profile.TryGetObject(out var info, "characters", "pmc", "Info"))
        {
            var levels = info["PrestigeLevels"] as JsonObject;
            var level = Math.Max(
                info["PrestigeLevel"]?.GetValue<int>() ?? 0,
                Math.Max(levels?[PrestigeGameModes.Regular]?.GetValue<int>() ?? 0, levels?[PrestigeGameModes.Pve]?.GetValue<int>() ?? 0)
            );

            info["PrestigeLevels"] = new JsonObject { [PrestigeGameModes.Regular] = 0, [PrestigeGameModes.Pve] = level };

            // Show the PvE prestige, the regular one is now empty
            info["SelectedPrestigeGameMode"] = PrestigeGameModes.Pve;
            info["PrestigeGameMode"] = PrestigeGameModes.Pve;
            info["PrestigeLevel"] = level;
        }

        return base.Migrate(profile);
    }
}
