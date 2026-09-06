using System;
using System.IO;

namespace Common
{
    // Item templates come straight from the server database. The repo root is found by walking up
    // from the build output.
    public static class ServerAssets
    {
        private static string _database;

        public static string Database
        {
            get
            {
                if (_database != null)
                {
                    return _database;
                }

                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Libraries", "SPTushonka.Server.Assets", "SPT_Data", "database");
                    if (Directory.Exists(candidate))
                    {
                        _database = candidate;
                        return _database;
                    }

                    dir = dir.Parent;
                }

                throw new Exception($"Could not find the server database above {AppContext.BaseDirectory}");
            }
        }
    }
}
