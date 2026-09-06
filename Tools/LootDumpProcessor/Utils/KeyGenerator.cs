namespace LootDumpProcessor.Utils;

public class KeyGenerator
{
    private static long currentKey = 0L;
    private static object lockObject = new();

    public static string GetNextKey()
    {
        lock (lockObject)
        {
            return $"{++currentKey}";
        }
    }
}
