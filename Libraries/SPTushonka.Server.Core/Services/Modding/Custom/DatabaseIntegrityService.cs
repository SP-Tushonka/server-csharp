using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Exceptions.Database;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTarkov.Server.Core.Services.Modding.Custom;

// Runs after everything else has loaded, so anything added past the cutoff is there to find
[Injectable(InjectionType.Singleton, TypePriority = int.MaxValue)]
public sealed class DatabaseIntegrityService(TemplateTable templateTable) : IOnLoad
{
    private HashSet<MongoId>? _itemIdsWhenProfilesLoaded;
    private bool _reported;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        EnsureNoItemsAddedSinceProfilesLoaded();

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Record which items existed at the point profiles began loading
    /// </summary>
    public void TakeItemSnapshot()
    {
        _itemIdsWhenProfilesLoaded = [.. templateTable.Items.Keys];
    }

    /// <summary>
    ///     Throw if items have appeared since the snapshot was taken
    /// </summary>
    /// <exception cref="DatabaseModifiedAfterCutoffException"> Thrown when items were added after profiles loaded </exception>
    public void EnsureNoItemsAddedSinceProfilesLoaded()
    {
        if (_itemIdsWhenProfilesLoaded is null || _reported)
        {
            return;
        }

        // Also runs on the save timer and passes almost every time, so walk the keys rather than build anything
        var itemWasAdded = false;
        foreach (var itemId in templateTable.Items.Keys)
        {
            if (_itemIdsWhenProfilesLoaded.Contains(itemId))
            {
                continue;
            }

            itemWasAdded = true;
            break;
        }

        if (!itemWasAdded)
        {
            return;
        }

        _reported = true;

        var addedItemIds = templateTable.Items.Keys.Where(itemId => !_itemIdsWhenProfilesLoaded.Contains(itemId)).ToList();

        throw new DatabaseModifiedAfterCutoffException(
            $"{addedItemIds.Count} item(s) were added to the database after profiles loaded: {string.Join(", ", addedItemIds.Take(10))}"
                + $"{(addedItemIds.Count > 10 ? ", ..." : "")}. "
                + $"Add items during {nameof(OnLoadOrder)}.{nameof(OnLoadOrder.Preload)}, "
                + "the database is fully loaded by then and lives for the rest of the server's lifetime"
        );
    }
}
