using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Location;
using SPTarkov.Server.Core.Models.Spt.Servers;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable]
public class LocationCallbacks(HttpResponseUtil httpResponseUtil, LocationController locationController)
{
    /// <summary>
    ///     Handle client/locations
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetLocationData(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(locationController.GenerateAll(sessionID)));
    }

    /// <summary>
    ///     Handle client/airdrop/loot
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> GetAirdropLoot(string url, GetAirdropLootRequest? info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(locationController.GetAirDropLoot(info)));
    }
}
