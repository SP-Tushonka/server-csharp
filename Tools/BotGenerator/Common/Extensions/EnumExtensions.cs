using System.Collections.Generic;
using Common.Models;

namespace Common.Extensions;

public static class EnumExtensions
{
    private static readonly List<BotType> bossTypes = new()
    {
        BotType.bossbully,
        BotType.bossgluhar,
        BotType.bosskilla,
        BotType.bosskojaniy,
        BotType.bosssanitar,
        BotType.bosstagilla,
        BotType.bossboar,
        BotType.bosskojaniy,
    };

    public static bool IsBoss(this BotType self)
    {
        return bossTypes.Contains(self);
    }
}
