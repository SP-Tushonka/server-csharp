using System.Text.Json;
using QuestVariableGenerator.Models;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

namespace QuestVariableGenerator;

/// <summary>Loads quest and variable group data and indexes the conditions used to rank candidate quests.</summary>
public class ServerData
{
    private const string GlobalVariableCondition = "GlobalVariableValue";

    public Dictionary<MongoId, Quest> Quests { get; }

    public Dictionary<MongoId, List<MongoId>> Groups { get; }

    /// <summary>Maps each variable member to its group.</summary>
    public Dictionary<MongoId, MongoId> MemberGroup { get; }

    /// <summary>Members referenced by dialogue actions or global variable rewards, which the server already handles.</summary>
    public HashSet<MongoId> HandledMembers { get; }

    /// <summary>GlobalVariableValue start conditions targeting a variable group, indexed by group ID.</summary>
    public Dictionary<MongoId, List<GatedQuest>> GatedOnGroup { get; }

    /// <summary>GlobalVariableValue start conditions targeting individual variables, including trader first-visit flags.</summary>
    public List<GatedQuest> GatedOnVariable { get; }

    /// <summary>Quests without start conditions, used as entry-quest candidates.</summary>
    public List<MongoId> Unconditional { get; }

    /// <summary>Groups referenced by quest conditions that contain at least one member without a known dialogue or reward handler.</summary>
    public List<MongoId> GroupsToSolve { get; }

    private readonly Dictionary<string, string> _names;

    public ServerData(JsonUtil json, string database)
    {
        Quests = json.DeserializeFromFile<Dictionary<MongoId, Quest>>(Path.Combine(database, "templates", "quests.json"))!;
        Groups = json.DeserializeFromFile<List<VariableGroupData>>(Path.Combine(database, "templates", "variableGroups.json"))!
            .ToDictionary(group => group.Id, group => group.Variables);
        _names = json.DeserializeFromFile<Dictionary<string, string>>(Path.Combine(database, "locales", "global", "en.json"))!;
        MemberGroup = [];
        foreach (var (group, members) in Groups)
        {
            foreach (var member in members)
            {
                MemberGroup[member] = group;
            }
        }

        HandledMembers = FindHandledMembers(Path.Combine(database, "templates", "dialogue.json"));

        var gated = Quests
            .SelectMany(quest => (quest.Value.Conditions.AvailableForStart ?? []).Select(condition => (quest.Key, condition)))
            .Where(x => x.condition.ConditionType == GlobalVariableCondition && Target(x.condition) is not null)
            .Select(x => new GatedQuest
            {
                Quest = x.Key,
                Target = Target(x.condition)!.Value,
                ConditionCreated = Created(x.condition.Id),
            })
            .ToList();
        GatedOnGroup = gated
            .Where(gate => Groups.ContainsKey(gate.Target))
            .GroupBy(gate => gate.Target)
            .ToDictionary(g => g.Key, g => g.ToList());
        GatedOnVariable = gated.Where(gate => !Groups.ContainsKey(gate.Target)).ToList();
        Unconditional = Quests
            .Where(quest => (quest.Value.Conditions.AvailableForStart ?? []).Count == 0)
            .Select(quest => quest.Key)
            .ToList();

        GroupsToSolve = Quests
            .Values.SelectMany(quest =>
                (quest.Conditions.AvailableForStart ?? [])
                    .Concat(quest.Conditions.AvailableForFinish ?? [])
                    .Concat(quest.Conditions.Fail ?? [])
            )
            .Where(condition => condition.ConditionType == GlobalVariableCondition)
            .Select(Target)
            .Where(target => target is not null && Groups.ContainsKey(target.Value))
            .Select(target => target!.Value)
            .Distinct()
            .Where(group => Groups[group].Any(member => !HandledMembers.Contains(member)))
            .ToList();
    }

    public string Name(MongoId quest)
    {
        var name = _names.GetValueOrDefault($"{quest} name");
        return string.IsNullOrEmpty(name) ? quest : name;
    }

    /// <summary>Extracts the creation timestamp in Unix seconds from the first four bytes of a MongoId.</summary>
    public static long Created(MongoId id)
    {
        return Convert.ToInt64(id.ToString()[..8], 16);
    }

    private static MongoId? Target(QuestCondition condition)
    {
        var target = condition.Target?.IsList == true ? condition.Target.List!.FirstOrDefault() : condition.Target?.Item;
        if (target is null || !MongoId.IsValidMongoId(target))
        {
            return null;
        }

        return new MongoId(target);
    }

    private HashSet<MongoId> FindHandledMembers(string dialoguePath)
    {
        var handled = Quests
            .Values.SelectMany(quest => quest.Rewards?.Values.SelectMany(rewards => rewards) ?? [])
            .Where(reward =>
                reward.Type == RewardType.GlobalVariable && reward.Target is not null && MemberGroup.ContainsKey(reward.Target)
            )
            .Select(reward => new MongoId(reward.Target!))
            .ToHashSet();

        // Dialogue lines are untyped in the server model, so inspect their actions directly in the JSON.
        using var dialogue = DumpReader.ParseFile(dialoguePath);
        foreach (
            var line in dialogue
                .RootElement.GetProperty("elements")
                .EnumerateArray()
                .SelectMany(element => element.GetProperty("Lines").EnumerateArray())
        )
        {
            if (!line.TryGetProperty("Actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var action in actions.EnumerateArray())
            {
                var id = action.TryGetProperty("variableId", out var variable) ? variable.GetString() : null;
                if (id is not null && MongoId.IsValidMongoId(id) && MemberGroup.ContainsKey(id))
                {
                    handled.Add(id);
                }
            }
        }

        return handled;
    }
}
