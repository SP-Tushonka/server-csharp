using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Helpers.Bot;

/// <summary>
///     Applies recorded item builds, the fixed weapon and gear setups a bot type is seen with, to generated items
/// </summary>
[Injectable]
public class BotItemBuildHelper(
    ItemHelper itemHelper,
    WeightedRandomHelper weightedRandomHelper,
    BotGeneratorHelper botGeneratorHelper,
    BotWeaponGeneratorHelper botWeaponGeneratorHelper,
    BotEquipmentModGenerator botEquipmentModGenerator
)
{
    /// <summary>
    ///     Pick a build weighted by how often it was seen
    /// </summary>
    /// <param name="builds">Builds to choose from</param>
    /// <returns>Chosen build</returns>
    public BotItemBuild PickBuild(IReadOnlyList<BotItemBuild> builds)
    {
        Dictionary<int, double> weights = [];
        for (var i = 0; i < builds.Count; i++)
        {
            weights[i] = builds[i].Weight;
        }

        return builds[weightedRandomHelper.GetWeightedValue(weights)];
    }

    /// <summary>
    ///     Check every part of a build against items the bot already has
    /// </summary>
    /// <param name="build">Build to check</param>
    /// <param name="currentItems">Items already on the bot</param>
    /// <returns>True when no part conflicts</returns>
    public bool IsBuildCompatible(BotItemBuild build, List<Item> currentItems)
    {
        return build
            .Items.Skip(1)
            .All(part =>
                !botGeneratorHelper.IsItemIncompatibleWithCurrentItems(currentItems, part.Template, part.SlotId).Incompatible.GetValueOrDefault(false)
            );
    }

    /// <summary>
    ///     Append the parts of a build below an existing root item, with fresh ids
    /// </summary>
    /// <param name="build">Build whose parts are added</param>
    /// <param name="rootId">Id of the generated root item the parts attach to</param>
    /// <param name="items">List the parts are appended to</param>
    /// <param name="modPool">Bot type mod pool, used for the rounds of cylinder magazines</param>
    /// <param name="botRole">Role of the bot the item is for</param>
    public void AddBuildParts(BotItemBuild build, MongoId rootId, List<Item> items, GlobalMods modPool, string botRole)
    {
        Dictionary<string, MongoId> newIds = [];
        newIds[build.Items[0].Id.ToString()] = rootId;
        foreach (var part in build.Items.Skip(1))
        {
            var template = itemHelper.GetItem(part.Template).Value;
            if (template is null || part.ParentId is null || !newIds.TryGetValue(part.ParentId, out var parentId))
            {
                continue;
            }

            var id = new MongoId();
            newIds[part.Id.ToString()] = id;
            items.Add(
                new Item
                {
                    Id = id,
                    Template = part.Template,
                    ParentId = parentId,
                    SlotId = part.SlotId,
                    Upd = botGeneratorHelper.GenerateExtraPropertiesForItem(template, botRole),
                }
            );

            // Cylinder magazines hold their rounds in camora slots, which magazine filling does not reach
            var parentTemplate = itemHelper.GetItem(template.Parent).Value;
            if (parentTemplate is not null && botWeaponGeneratorHelper.MagazineIsCylinderRelated(parentTemplate.Name))
            {
                botEquipmentModGenerator.FillCamora(items, modPool, id, template);
            }
        }
    }
}
