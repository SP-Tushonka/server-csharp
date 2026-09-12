using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using Path = System.IO.Path;

namespace QuestVariableGenerator;

/// <summary>
///     Builds questVariables.json, the list of which quest sets which story flag, from live captures placed in
///     ./input. Live never tells the client this, so it has to be read off real accounts. Output goes to ./output.
/// </summary>
public static class Program
{
    public static void Main()
    {
        var input = Path.Combine(Directory.GetCurrentDirectory(), "input");
        var output = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(output);

        var json = new JsonUtil([new SptJsonConverterRegistrator()]);
        var data = new ServerData(json, ServerDatabase());
        var dumps = new DumpReader(data).Read(input);
        Console.WriteLine($"{dumps.LivePairs.Count} live pairs, {dumps.Snapshots.Count} profile snapshots since the back fill");

        var result = new GroupSolver(data, dumps).Solve();
        File.WriteAllText(Path.Combine(output, "questVariables.json"), json.Serialize(result, true));

        var members = result.Keys.Sum(group => data.Groups[group].Count);
        Console.WriteLine(
            $"{result.Count} groups, {result.Values.Sum(g => g.Quests.Count)} of {members} members pinned, "
                + $"{result.Values.Sum(g => g.Fillers.Sum(f => f.Members))} members left to filler classes, "
                + $"{result.Values.Sum(g => g.NotMembers.Count)} gated quests that are not members"
        );
    }

    private static string ServerDatabase()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Libraries", "SPTushonka.Server.Assets", "SPT_Data", "database");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new Exception($"Could not find the server database above {AppContext.BaseDirectory}");
    }
}
