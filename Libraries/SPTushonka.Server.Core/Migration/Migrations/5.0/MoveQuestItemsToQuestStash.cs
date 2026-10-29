using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Eft.Profile;

namespace SPTarkov.Server.Core.Migration.Migrations._5._0;

/// <summary>
///     Hideout crafts on earlier 5.0 builds put quest items, notes and tapes in the main stash. Move them into the quest
///     stash.
/// </summary>
[Injectable]
public sealed class MoveQuestItemsToQuestStash(IServiceProvider serviceProvider) : AbstractProfileMigration
{
    public override string MigrationName
    {
        get { return "MoveQuestItemsToQuestStash500"; }
    }

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        if (
            !profile.TryGetObject(out var inventory, "characters", "pmc", "Inventory")
            || !inventory.TryGetValue<string>(out var stashId, "stash")
            || !inventory.TryGetValue<string>(out _, "questStashItems")
            || !inventory.TryGetArray(out var items, "items")
        )
        {
            return false;
        }

        var parents = new Dictionary<string, string?>();
        var templates = new Dictionary<string, string>();
        foreach (var item in items)
        {
            if (item.TryGetValue<string>(out var id, "_id") && item.TryGetValue<string>(out var template, "_tpl"))
            {
                parents[id] = item.TryGetValue<string>(out var parentId, "parentId") ? parentId : null;
                templates[id] = template;
            }
        }

        var inventoryHelper = GetInventoryHelper();

        return templates.Any(item => inventoryHelper.BelongsInQuestStash(item.Value) && IsInsideStash(item.Key, parents, stashId));
    }

    public override bool PostMigrate(SptProfile profile)
    {
        var pmc = profile.CharacterData?.PmcData;
        var inventory = pmc?.Inventory;
        if (inventory?.Items is null || inventory.Stash is null || inventory.QuestStashItems is null)
        {
            return true;
        }

        var inventoryHelper = GetInventoryHelper();
        var parents = new Dictionary<string, string?>();
        foreach (var item in inventory.Items)
        {
            parents[item.Id.ToString()] = item.ParentId;
        }

        var stashId = inventory.Stash.Value.ToString();
        var misplaced = inventory
            .Items.Where(item => inventoryHelper.BelongsInQuestStash(item.Template) && IsInsideStash(item.Id.ToString(), parents, stashId))
            .ToList();

        foreach (var item in misplaced)
        {
            inventoryHelper.PlaceItemInQuestStash(pmc!, inventory.Items.GetItemWithChildren(item.Id));
        }

        return true;
    }

    // InventoryHelper is resolved here because profile loading sits below it
    private InventoryHelper GetInventoryHelper()
    {
        return (serviceProvider.GetService(typeof(InventoryHelper)) as InventoryHelper)!;
    }

    /// <summary>
    ///     Check whether an item sits somewhere below the main stash
    /// </summary>
    /// <param name="itemId">Item to check</param>
    /// <param name="parents">Parent id of every inventory item</param>
    /// <param name="stashId">Main stash id</param>
    /// <returns>True when the main stash holds the item</returns>
    private static bool IsInsideStash(string itemId, Dictionary<string, string?> parents, string stashId)
    {
        // Migrations run before the profile fixer breaks circular parent references
        HashSet<string> visited = [];
        var current = itemId;
        while (visited.Add(current) && parents.TryGetValue(current, out var parentId) && parentId is not null)
        {
            if (parentId == stashId)
            {
                return true;
            }

            current = parentId;
        }

        return false;
    }
}
