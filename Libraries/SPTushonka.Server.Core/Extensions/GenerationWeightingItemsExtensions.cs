using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace SPTarkov.Server.Core.Extensions;

public static class GenerationWeightingItemsExtensions
{
    /// <summary>
    ///     Order containers by a weighted draw on where the role keeps this kind of item, the rest follow in their original order
    /// </summary>
    /// <param name="weights">Generation weights of the bot</param>
    /// <param name="kind">Loot category or weapon slot</param>
    /// <param name="available">Containers the item may go into</param>
    /// <param name="weightedRandomHelper">Helper to draw containers with</param>
    /// <returns>Containers to try in order</returns>
    public static HashSet<EquipmentSlots> OrderByPlacement(
        this GenerationWeightingItems? weights,
        string kind,
        IEnumerable<EquipmentSlots> available,
        WeightedRandomHelper weightedRandomHelper
    )
    {
        var allowed = available.ToList();
        var shares = new Dictionary<EquipmentSlots, double>();
        foreach (var (name, share) in weights?.Placement?.GetValueOrDefault(kind) ?? [])
        {
            if (share > 0 && Enum.TryParse<EquipmentSlots>(name, out var slot) && allowed.Contains(slot))
            {
                shares[slot] = share;
            }
        }

        var ordered = new List<EquipmentSlots>();
        while (shares.Count > 0)
        {
            var slot = weightedRandomHelper.GetWeightedValue(shares);
            ordered.Add(slot);
            shares.Remove(slot);
        }

        return [.. ordered.Concat(allowed)];
    }
}
