using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable]
public class EndingCallbacks(HttpResponseUtil httpResponseUtil, EndingController endingController)
{
    /// <summary>Handle /client/ending/obtain</summary>
    public async ValueTask<string> ObtainEnding(string url, EndingRequest info, MongoId sessionID)
    {
        await endingController.ObtainEnding(sessionID, info);

        return httpResponseUtil.NullResponse();
    }

    /// <summary>
    ///     Handle SetCurrentEnding event
    /// </summary>
    /// <param name="pmcData">Player's pmc profile</param>
    /// <param name="info">Ending to show</param>
    /// <param name="sessionID">Session/player id</param>
    /// <returns>Item event output</returns>
    public ValueTask<ItemEventRouterResponse> SetDisplayedEnding(PmcData pmcData, SetDisplayedEndingRequest info, MongoId sessionID)
    {
        return new ValueTask<ItemEventRouterResponse>(endingController.SetDisplayedEnding(pmcData, info, sessionID));
    }

    /// <summary>Handle /client/ending/localization</summary>
    public ValueTask<string> GetLocalization(string url, EndingRequest info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(endingController.GetLocalization(info)));
    }
}
