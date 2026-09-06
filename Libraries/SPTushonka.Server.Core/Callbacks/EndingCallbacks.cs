using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
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

    /// <summary>Handle /client/ending/localization</summary>
    public ValueTask<string> GetLocalization(string url, EndingRequest info, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(endingController.GetLocalization(info)));
    }
}
