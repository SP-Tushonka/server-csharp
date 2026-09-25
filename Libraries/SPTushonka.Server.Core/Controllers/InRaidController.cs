using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.InRaid;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class InRaidController(BotConfig botConfig, InRaidConfig inRaidConfig)
{
    /// <summary>
    ///     Save locationId to active profiles in-raid object AND app context
    /// </summary>
    /// <param name="sessionId">Session id</param>
    /// <param name="info">Register player request</param>
    public void AddPlayer(MongoId sessionId, RegisterPlayerRequestData info)
    {
        // _applicationContext.AddValue(ContextVariableType.REGISTER_PLAYER_REQUEST, info);
    }

    /// <summary>
    ///     Get the inraid config from configs/inraid.json
    /// </summary>
    public InRaidConfig GetInRaidConfig()
    {
        return inRaidConfig;
    }

    /// <summary>
    ///     Get all boss role types e.g. bossTagilla
    /// </summary>
    /// <param name="url"></param>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns>string array of boss types</returns>
    public List<string> GetBossTypes(string url, MongoId sessionId)
    {
        return botConfig.Bosses;
    }
}
