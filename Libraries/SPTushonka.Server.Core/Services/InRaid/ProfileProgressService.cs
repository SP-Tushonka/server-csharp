using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTarkov.Server.Core.Services.InRaid;

/// <summary>
///     Handles a location's profile progress options, e.g. Terminal removing quest items before the raid and
///     limiting what is saved from it
/// </summary>
[Injectable]
public class ProfileProgressService(ISptLogger<ProfileProgressService> logger, LocationTable locationTable)
{
    /// <summary>
    ///     Get the profile progress options of a location
    /// </summary>
    /// <param name="locationName">Location id e.g. Terminal</param>
    /// <returns>ProfileProgressOptions, null when the location has none</returns>
    public ProfileProgressOptions? GetOptions(string locationName)
    {
        return locationTable.GetLocation(locationName)?.Base?.ProfileProgressOptions;
    }

    /// <summary>
    ///     Remove the items listed in the location's removeItemListBeforeRaidStart from the profile, children included
    /// </summary>
    /// <param name="profile">Profile to remove items from</param>
    /// <param name="locationName">Location id e.g. Terminal</param>
    /// <returns>True when items were removed</returns>
    public bool RemoveListedItems(SptProfile profile, string locationName)
    {
        var listed = GetOptions(locationName)?.RemoveItemListBeforeRaidStart;
        var items = profile.CharacterData?.PmcData?.Inventory?.Items;
        if (listed is null || items is null)
        {
            return false;
        }

        var dropTpls = listed.Where(entry => entry.TemplateId is not null).Select(entry => new MongoId(entry.TemplateId!)).ToHashSet();
        var dropIds = items
            .Where(item => dropTpls.Contains(item.Template))
            .SelectMany(item => items.GetItemWithChildren(item.Id))
            .Select(item => item.Id)
            .ToHashSet();
        if (dropIds.Count == 0)
        {
            return false;
        }

        items.RemoveAll(item => dropIds.Contains(item.Id));
        logger.Info($"Dropped {dropIds.Count} listed item(s) before {locationName}");

        return true;
    }
}
