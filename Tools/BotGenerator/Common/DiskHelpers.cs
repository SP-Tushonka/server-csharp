using System.IO;

namespace Common;

public static class DiskHelpers
{
    public static void CreateDirIfDoesntExist(string path)
    {
        if (!Directory.Exists($"{path}"))
        {
            //create dump dir
            Directory.CreateDirectory($"{path}");
        }
    }
}
