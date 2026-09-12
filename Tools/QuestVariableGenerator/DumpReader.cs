using System.Text.Json;
using System.Text.RegularExpressions;
using QuestVariableGenerator.Models;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using Path = System.IO.Path;

namespace QuestVariableGenerator;

/// <summary>Reads captured profile lists and paired items/moving requests and responses from the input directory.</summary>
public class DumpReader(ServerData data)
{
    private const string QuestCompleteAction = "QuestComplete";

    /// <summary>Snapshot cutoff for the live backfill of variable members for previously completed quests.</summary>
    private const long BackfillTimestamp = 1788048000;

    private static readonly Regex _dumpTimestamp = new(@"_(\d+)\.json$", RegexOptions.Compiled);

    public DumpData Read(string input)
    {
        var livePairs = new Dictionary<MongoId, MongoId>();

        foreach (var requestPath in Directory.GetFiles(input, "req.client.game.profile.items.moving_*.json"))
        {
            ReadExchange(requestPath, livePairs);
        }

        var snapshots = Directory
            .GetFiles(input, "resp.client.game.profile.list_*.json")
            .Where(path => Timestamp(path) >= BackfillTimestamp)
            .SelectMany(ReadProfileList)
            .ToList();

        return new DumpData { LivePairs = livePairs, Snapshots = snapshots };
    }

    public static JsonDocument ParseFile(string path)
    {
        return JsonDocument.Parse(File.ReadAllText(path).TrimStart((char)0xFEFF));
    }

    /// <summary>
    ///     Associates group members in profileChanges.variableValues with the request's single QuestComplete action.
    ///     Excludes members already handled by dialogue actions or quest rewards.
    /// </summary>
    private void ReadExchange(string requestPath, Dictionary<MongoId, MongoId> livePairs)
    {
        var responsePath = Path.Combine(Path.GetDirectoryName(requestPath)!, Path.GetFileName(requestPath).Replace("req.", "resp."));
        if (!File.Exists(responsePath))
        {
            return;
        }

        // Some older captures contain request bodies saved before decryption.
        JsonDocument request;
        try
        {
            request = ParseFile(requestPath);
        }
        catch (JsonException)
        {
            return;
        }

        using (request)
        using (var response = ParseFile(responsePath))
        {
            var completed = request
                .RootElement.GetProperty("data")
                .EnumerateArray()
                .Where(action => action.GetProperty("Action").GetString() == QuestCompleteAction)
                .Select(action => new MongoId(action.GetProperty("qid").GetString()))
                .ToList();

            if (!response.RootElement.TryGetProperty("profileChanges", out var changes))
            {
                return;
            }

            foreach (var change in changes.EnumerateObject())
            {
                if (!change.Value.TryGetProperty("variableValues", out var variables) || variables.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var variable in variables.EnumerateObject().Where(v => MongoId.IsValidMongoId(v.Name)))
                {
                    var member = new MongoId(variable.Name);
                    if (!data.MemberGroup.ContainsKey(member) || data.HandledMembers.Contains(member))
                    {
                        continue;
                    }

                    if (completed.Count != 1)
                    {
                        Console.WriteLine(
                            $"  {member} set by {completed.Count} completions at once in {Path.GetFileName(requestPath)}, skipped"
                        );
                        continue;
                    }

                    livePairs[member] = completed[0];
                }
            }
        }
    }

    /// <summary>Extracts set variables and successful quests from each PMC profile in a profile list response.</summary>
    private static IEnumerable<ProfileSnapshot> ReadProfileList(string path)
    {
        using var document = ParseFile(path);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var payload))
        {
            root = payload;
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var profile in root.EnumerateArray())
        {
            if (profile.GetProperty("Info").GetProperty("Side").GetString() == "Savage")
            {
                continue;
            }

            var variables = new HashSet<MongoId>();
            if (profile.TryGetProperty("Variables", out var values) && values.ValueKind == JsonValueKind.Object)
            {
                variables.UnionWith(
                    values
                        .EnumerateObject()
                        .Where(v => v.Value.GetInt32() != 0 && MongoId.IsValidMongoId(v.Name))
                        .Select(v => new MongoId(v.Name))
                );
            }

            // Ignore quest IDs that cannot be represented as MongoIds, such as some repeatable quest IDs.
            var completed = profile
                .GetProperty("Quests")
                .EnumerateArray()
                .Where(quest => quest.GetProperty("status").GetInt32() == (int)QuestStatusEnum.Success)
                .Select(quest => quest.GetProperty("qid").GetString()!)
                .Where(MongoId.IsValidMongoId)
                .Select(id => new MongoId(id))
                .ToHashSet();

            yield return new ProfileSnapshot { Variables = variables, Completed = completed };
        }
    }

    private static long Timestamp(string path)
    {
        var match = _dumpTimestamp.Match(path);
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }
}
