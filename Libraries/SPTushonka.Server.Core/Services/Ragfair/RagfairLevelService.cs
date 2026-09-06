using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTarkov.Server.Core.Services.Ragfair;

[Injectable(InjectionType.Singleton)]
public class RagfairLevelService(TemplateTable templateTable, GlobalTable globalTable, ItemHelper itemHelper)
{
    private readonly Lock _lock = new();
    private Dictionary<MongoId, HandbookCategory>? _categories;
    private Dictionary<MongoId, MongoId>? _itemCategories;

    public bool Enabled
    {
        get { return globalTable.Configuration.RagFair.RagfairMinUserLevelByCategory; }
    }

    public bool IsLocked(IEnumerable<Item> items, int playerLevel, out int requiredLevel)
    {
        requiredLevel = 0;

        if (!Enabled)
        {
            return false;
        }

        var byId = items.ToDictionary(item => item.Id.ToString());
        var locked = false;

        foreach (var item in byId.Values)
        {
            var root = RootOf(item, byId);

            if (root != item && (JudgedByRootOnly(root) || IsPlate(item)))
            {
                continue;
            }

            if (!IsTemplateLocked(item.Template, playerLevel, out var level))
            {
                continue;
            }

            if (level < 0)
            {
                requiredLevel = -1;
                return true;
            }

            locked = true;
            requiredLevel = Math.Max(requiredLevel, level);
        }

        return locked;
    }

    public bool IsTemplateLocked(MongoId tpl, int playerLevel, out int level)
    {
        level = 0;
        EnsureCache();

        if (!_itemCategories!.TryGetValue(tpl, out var category))
        {
            return false;
        }

        level = itemHelper.GetItem(tpl).Value?.Properties?.RagfairLevelToTrade ?? 0;

        if (Locks(level, playerLevel))
        {
            return true;
        }

        MongoId? current = category;
        var depth = 0;

        while (current is not null && depth++ < 32 && _categories!.TryGetValue(current.Value, out var node))
        {
            level = node.RagfairLevelToTrade ?? 0;

            if (Locks(level, playerLevel))
            {
                return true;
            }

            current = node.ParentId;
        }

        return false;
    }

    private static bool Locks(int level, int playerLevel)
    {
        return level < 0 || level > playerLevel;
    }

    private static Item RootOf(Item item, Dictionary<string, Item> byId)
    {
        var depth = 0;

        while (item.ParentId is not null && depth++ < 64 && byId.TryGetValue(item.ParentId, out var parent))
        {
            item = parent;
        }

        return item;
    }

    private bool JudgedByRootOnly(Item root)
    {
        return itemHelper.IsOfBaseclasses(root.Template, [BaseClasses.WEAPON, BaseClasses.AMMO_BOX]) || IsPlate(root);
    }

    private bool IsPlate(Item item)
    {
        return itemHelper.IsOfBaseclasses(item.Template, [BaseClasses.ARMOR_PLATE, BaseClasses.BUILT_IN_INSERTS]);
    }

    private void EnsureCache()
    {
        if (_itemCategories is not null)
        {
            return;
        }

        lock (_lock)
        {
            if (_itemCategories is not null)
            {
                return;
            }

            var handbook = templateTable.Handbook;
            _categories = handbook.Categories.ToDictionary(category => category.Id);
            _itemCategories = handbook.Items.ToDictionary(item => item.Id, item => item.ParentId);
        }
    }
}
