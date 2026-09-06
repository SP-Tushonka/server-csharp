using System.Linq;

namespace Common;

public static class BulletHelpers
{
    private static readonly string[] blackList = { };

    public static bool BulletIsOnBlackList(string bullet)
    {
        return blackList.Any(x => x.Contains(bullet));
    }
}
