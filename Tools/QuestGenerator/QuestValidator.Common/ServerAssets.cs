using System;
using System.IO;

namespace QuestValidator.Common
{
    // The tools read item templates and locales straight from the server's database instead of
    // carrying their own copies. The repo root is found by walking up from the build output.
    public static class ServerAssets
    {
        private static string _database;

        public static string Database
        {
            get
            {
                if (_database is not null)
                {
                    return _database;
                }

                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null)
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
