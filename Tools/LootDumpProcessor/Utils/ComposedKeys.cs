using LootDumpProcessor.Model.Processing;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Utils;

public static class ComposedKeys
{
    // Ammo is excluded so magazine contents do not split otherwise identical spawns into separate keys.
    public static ComposedKey Create(IEnumerable<SptLootItem> items)
    {
        if (items == null)
        {
            return new ComposedKey { Key = KeyGenerator.GetNextKey() };
        }

        var tarkovItems = LootDumpProcessorContext.GetTarkovItems();
        var sum = 0d;
        foreach (var item in items)
        {
            var tpl = item.Template.ToString();
            if (tpl.Length == 0 || tarkovItems.IsBaseClass(tpl, BaseClasses.Ammo))
            {
                continue;
            }

            sum += tpl.GetHashCode();
        }

        return new ComposedKey { Key = sum.ToString() };
    }
}
