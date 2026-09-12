using QuestVariableGenerator.Models;
using SPTarkov.Server.Core.Models.Common;

namespace QuestVariableGenerator;

/// <summary>
///     Works out which quest sets which flag of a group. A flag and a quest belong together when every captured
///     profile has both or neither. Where that still leaves several options, the flags are listed in the order the
///     quests were given their gates, which breaks the tie.
/// </summary>
public class GroupSolver(ServerData data, DumpData dumps)
{
    /// <summary>Each quest's completion pattern across the input snapshots.</summary>
    private readonly Dictionary<MongoId, string> _questPatterns = data.Quests.Keys.ToDictionary(
        quest => quest,
        quest => Pattern(dumps.Snapshots, snapshot => snapshot.Completed.Contains(quest))
    );

    /// <summary>Quests excluded from further inference because they already have an observed or inferred pair.</summary>
    private readonly HashSet<MongoId> _assigned = [.. dumps.LivePairs.Values];

    public Dictionary<MongoId, QuestVariableGroup> Solve()
    {
        return data.GroupsToSolve.ToDictionary(group => group, SolveGroup);
    }

    private QuestVariableGroup SolveGroup(MongoId group)
    {
        var members = data.Groups[group];
        var result = new QuestVariableGroup { Id = group };
        foreach (var (member, quest) in dumps.LivePairs.Where(pair => data.MemberGroup[pair.Key] == group))
        {
            result.Quests[quest] = member;
        }

        var candidates = Candidates(group);
        var open = members.Where(member => !data.HandledMembers.Contains(member) && !result.Quests.ContainsValue(member));
        foreach (var patternClass in open.GroupBy(member => Pattern(dumps.Snapshots, snapshot => snapshot.Variables.Contains(member))))
        {
            var classMembers = patternClass.ToList();
            var expected = candidates.Expected.Where(quest => Fits(quest, patternClass.Key)).ToList();
            var fitting = expected.Concat(candidates.SameTrader.Where(quest => Fits(quest, patternClass.Key))).ToList();
            if (fitting.Count < classMembers.Count)
            {
                fitting.AddRange(candidates.Others.Where(quest => Fits(quest, patternClass.Key)));
            }

            // Prefer the expected candidates when their count matches; otherwise require all fitting candidates to match.
            var pins =
                expected.Count == classMembers.Count ? expected
                : fitting.Count == classMembers.Count ? fitting
                : null;
            if (pins is not null)
            {
                for (var i = 0; i < classMembers.Count; i++)
                {
                    result.Quests[pins[i]] = classMembers[i];
                    _assigned.Add(pins[i]);
                }
            }
            else if (fitting.Count > 0)
            {
                // The consumer treats an undersized candidate set as setting the entire class on any completion.
                if (fitting.Count < classMembers.Count)
                {
                    Console.WriteLine($"  group {group}: {classMembers.Count} members fit only {fitting.Count} quests");
                }

                result.Fillers.Add(new FillerClass { Members = classMembers.Count, Quests = fitting });
            }
            else
            {
                Console.WriteLine($"  group {group}: {classMembers.Count} members fit no quest at all");
            }
        }

        var memberPatterns = members
            .Select(member => Pattern(dumps.Snapshots, snapshot => snapshot.Variables.Contains(member)))
            .ToHashSet();
        result.NotMembers.AddRange(
            data.GatedOnGroup.GetValueOrDefault(group, [])
                .Select(gate => gate.Quest)
                .Where(quest =>
                    !result.Quests.ContainsKey(quest) && !_assigned.Contains(quest) && !memberPatterns.Contains(_questPatterns[quest])
                )
        );
        foreach (var quest in result.NotMembers)
        {
            Console.WriteLine($"  {data.Name(quest)} is gated on group {group} but sets none of its members");
        }

        return result;
    }

    /// <summary>
    ///     Ranks candidates by their relationship to the group: entry quests and quests gated on the group,
    ///     other quests from the same traders, then all remaining quests. When no trader is identified by a
    ///     group gate, the trader filter accepts every quest.
    /// </summary>
    private CandidateQuests Candidates(MongoId group)
    {
        var gated = data.GatedOnGroup.GetValueOrDefault(group, []);
        var traders = gated.Select(gate => data.Quests[gate.Quest].TraderId).ToHashSet();

        // Use the quest ID's timestamp when no start condition is available to date an entry quest.
        var entry = data
            .Unconditional.Where(quest => SameTrader(traders, quest))
            .Select(quest => new GatedQuest
            {
                Quest = quest,
                Target = MongoId.Empty(),
                ConditionCreated = ServerData.Created(quest),
            })
            .Concat(data.GatedOnVariable.Where(gate => SameTrader(traders, gate.Quest)))
            .OrderBy(gate => gate.ConditionCreated)
            .Select(gate => gate.Quest);
        var expected = entry.Concat(gated.OrderBy(gate => gate.ConditionCreated).Select(gate => gate.Quest)).Distinct().ToList();
        var sameTrader = data
            .Quests.Keys.Where(quest => SameTrader(traders, quest) && !expected.Contains(quest))
            .OrderBy(ServerData.Created)
            .ToList();
        var others = data
            .Quests.Keys.Where(quest => !expected.Contains(quest) && !sameTrader.Contains(quest))
            .OrderBy(ServerData.Created)
            .ToList();

        return new CandidateQuests
        {
            Expected = expected,
            SameTrader = sameTrader,
            Others = others,
        };
    }

    /// <summary>Accepts quests from the gating traders, or any quest when no gating trader is known.</summary>
    private bool SameTrader(HashSet<MongoId> traders, MongoId quest)
    {
        return traders.Count == 0 || traders.Contains(data.Quests[quest].TraderId);
    }

    private bool Fits(MongoId quest, string memberPattern)
    {
        return _questPatterns[quest] == memberPattern && !_assigned.Contains(quest);
    }

    private static string Pattern(List<ProfileSnapshot> snapshots, Func<ProfileSnapshot, bool> test)
    {
        return string.Concat(snapshots.Select(snapshot => test(snapshot) ? '1' : '0'));
    }
}
