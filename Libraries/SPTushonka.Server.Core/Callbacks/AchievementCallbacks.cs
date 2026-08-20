using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Servers;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable]
public class AchievementCallbacks(AchievementController achievementController, HttpResponseUtil httpResponseUtil)
{
    /// <summary>
    ///     Handle client/achievement/list
    /// </summary>
    /// <returns></returns>
    public ValueTask<StreamedJsonBody> GetAchievements(string url, GetAchievementListRequest _, MongoId sessionID)
    {
        return new ValueTask<StreamedJsonBody>(httpResponseUtil.GetStreamedBody(achievementController.GetAchievements(sessionID)));
    }

    /// <summary>
    ///     Handle client/achievement/statistic
    /// </summary>
    /// <returns></returns>
    public ValueTask<string> Statistic(string url, EmptyRequestData _, MongoId sessionID)
    {
        return new ValueTask<string>(httpResponseUtil.GetBody(achievementController.GetAchievementStatics(sessionID)));
    }
}
