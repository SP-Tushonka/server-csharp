using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using GlobalConfig = SPTarkov.Server.Core.Models.Spt.Tables.GlobalConfig;

namespace SPTarkov.Server.Core.Services.Server.SeasonalEvents;

[Injectable(InjectionType.Singleton)]
public class ChristmasEventApplier(SeasonalEventEffects effects, LocationTable locationTable) : ISeasonalEventApplier
{
    /// <summary>Static container holding the christmas sleigh loot</summary>
    private const string SleighContainerId = "67614e3a6a90e4f10b0b140d";

    public SeasonalEventType Type
    {
        get { return SeasonalEventType.Christmas; }
    }

    public IEnumerable<string> GiftKeys
    {
        get { return ["Christmas2022"]; }
    }

    public void Apply(SeasonalEvent seasonalEvent, GlobalConfig globalConfig)
    {
        if (seasonalEvent.Settings?.EnableChristmasHideout ?? false)
        {
            effects.AddClientEventTypes(globalConfig, EventType.Christmas);
        }

        effects.AddEventGearToBots(Type);
        effects.AddEventLootToBots(Type);

        if (seasonalEvent.Settings?.EnableSanta ?? false)
        {
            effects.AddGifterBotToMaps();
            effects.AddLootItemsToGifterDropItemsList();
        }

        if (seasonalEvent.Settings?.AdjustBotAppearances ?? false)
        {
            effects.AdjustBotAppearanceValues(Type);
        }

        BoostChristmasLoot(seasonalEvent.Settings?.ChristmasLootBoostAmount ?? 0.03);
        effects.ChangeBtrToTarColaSkin();
    }

    /// <summary>
    ///     Drop the sleigh's empty roll and raise the chance on christmas loose loot
    /// </summary>
    /// <param name="boostAmount">Share of the remaining probability to add to each christmas spawn</param>
    private void BoostChristmasLoot(double boostAmount)
    {
        foreach (var location in locationTable.GetDictionary())
        {
            location.Value.StaticLoot?.AddTransformer(staticLootData =>
            {
                if (staticLootData is null)
                {
                    return staticLootData;
                }

                if (staticLootData.TryGetValue(SleighContainerId, out var sleighContainer))
                {
                    sleighContainer.ItemCountDistribution = sleighContainer.ItemCountDistribution.Where(d => d.Count != 0).ToList();
                }

                return staticLootData;
            });

            location.Value.LooseLoot?.AddTransformer(looseLootData =>
            {
                if (looseLootData?.Spawnpoints is null)
                {
                    return looseLootData;
                }

                foreach (var spawnPoint in looseLootData.Spawnpoints)
                {
                    if (spawnPoint.Template?.Id is null || !spawnPoint.Template.Id.Contains("christmas", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var probability = spawnPoint.Probability ?? 0.0;
                    spawnPoint.Probability = Math.Min(1.0, probability + (1.0 - probability) * boostAmount);
                }

                return looseLootData;
            });
        }
    }
}
