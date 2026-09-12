using System.Text.Json;
using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

namespace UnitTests.Tests.Helpers;

// Replays captured live accounts through QuestVariableHelper and compares with what live set.
// Skipped when the captures are not on this machine.
[TestFixture]
public class QuestVariableReplayTests
{
    private const string Acid = @"F:\User Profile\Downloads\eft-1.1-dumps-main\gw-pve.escapefromtarkov.com\client\game\profile";
    private const string OldDumps = @"F:\User Profile\Downloads\1.0 dumps";

    private QuestVariableHelper _helper = default!;
    private TemplateTable _templates = default!;

    [SetUp]
    public void Setup()
    {
        Assume.That(Directory.Exists(Acid) && Directory.Exists(OldDumps), "live captures are not available");
        _helper = DI.GetInstance().GetService<QuestVariableHelper>();
        _templates = DI.GetInstance().GetService<TemplateTable>();
    }

    [Test]
    public void Replay_AcidStepByStep_ReportsSameVariablesAsLive()
    {
        var pmc = new PmcData { Quests = [], Variables = [] };
        var solved = _templates.QuestVariables.Keys.ToHashSet();
        var members = _templates.VariableGroups.Where(g => solved.Contains(g.Id)).SelectMany(g => g.Variables).ToHashSet();
        var steps = 0;
        var mismatches = new List<string>();

        foreach (var requestPath in Directory.GetFiles(Path.Combine(Acid, "items", "moving", "request")).OrderBy(p => p))
        {
            var responsePath = Path.Combine(Acid, "items", "moving", "response", Path.GetFileName(requestPath).Replace("req.", "resp."));
            using var request = JsonDocument.Parse(File.ReadAllText(requestPath).TrimStart((char)0xFEFF));
            using var response = JsonDocument.Parse(File.ReadAllText(responsePath).TrimStart((char)0xFEFF));
            var completed = request
                .RootElement.GetProperty("data")
                .EnumerateArray()
                .Where(a => a.GetProperty("Action").GetString() == "QuestComplete")
                .Select(a => new MongoId(a.GetProperty("qid").GetString()))
                .ToList();
            var live = new Dictionary<MongoId, int>();
            foreach (var change in response.RootElement.GetProperty("profileChanges").EnumerateObject())
            {
                if (change.Value.TryGetProperty("variableValues", out var v) && v.ValueKind == JsonValueKind.Object)
                {
                    foreach (var entry in v.EnumerateObject().Where(e => members.Contains(e.Name)))
                    {
                        live[new MongoId(entry.Name)] = entry.Value.GetInt32();
                    }
                }
            }

            foreach (var quest in completed)
            {
                pmc.Quests!.Add(
                    new QuestStatus
                    {
                        QId = quest,
                        Status = QuestStatusEnum.Success,
                        StartTime = 0,
                        StatusTimers = [],
                    }
                );
                var changes = new ProfileChange();
                _helper.RecordCompletion(pmc, quest, changes);
                steps++;
                var ours = (changes.VariableValues ?? []).Where(kv => members.Contains(kv.Key)).ToDictionary();
                if (!ours.Keys.ToHashSet().SetEquals(live.Keys))
                {
                    mismatches.Add(
                        $"{Path.GetFileName(requestPath)} quest {quest}: live {string.Join(",", live.Keys)} ours {string.Join(",", ours.Keys)}"
                    );
                }
            }
        }

        TestContext.Out.WriteLine($"{steps} completions replayed");
        Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
    }

    [Test]
    public void SyncAll_OldAccounts_ReproducesLiveGroupSums()
    {
        var report = new List<string>();
        foreach (var account in Directory.GetDirectories(OldDumps).Where(d => !d.EndsWith("EFU")))
        {
            var latest = Directory
                .GetFiles(account, "resp.client.game.profile.list_*.json", SearchOption.AllDirectories)
                .OrderBy(p => p)
                .LastOrDefault();
            if (latest is null)
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(latest).TrimStart((char)0xFEFF));
            var profile = document
                .RootElement.EnumerateArray()
                .First(p => p.GetProperty("Info").GetProperty("Side").GetString() != "Savage");
            var live = profile
                .GetProperty("Variables")
                .EnumerateObject()
                .Where(v => MongoId.IsValidMongoId(v.Name))
                .ToDictionary(v => new MongoId(v.Name), v => v.Value.GetInt32());
            var pmc = new PmcData
            {
                Variables = [],
                Quests = profile
                    .GetProperty("Quests")
                    .EnumerateArray()
                    .Where(q => MongoId.IsValidMongoId(q.GetProperty("qid").GetString()!))
                    .Select(q => new QuestStatus
                    {
                        QId = new MongoId(q.GetProperty("qid").GetString()),
                        Status = (QuestStatusEnum)q.GetProperty("status").GetInt32(),
                        StartTime = 0,
                        StatusTimers = [],
                    })
                    .ToList(),
            };
            _helper.SyncAll(pmc);

            foreach (var group in _templates.QuestVariables.Values)
            {
                var members = _templates.VariableGroups.First(g => g.Id == group.Id).Variables;
                var liveSum = members.Sum(m => live.GetValueOrDefault(m) != 0 ? 1 : 0);
                var ourSum = members.Sum(m => pmc.Variables.GetValueOrDefault(m) != 0 ? 1 : 0);
                var exactWrong = group.Quests.Values.Count(m =>
                    (live.GetValueOrDefault(m) != 0) != (pmc.Variables.GetValueOrDefault(m) != 0)
                );
                // a group live never touched on that account says nothing, ru1 completed Introduction without its member
                // two members of Prapor's 18 member group are quest rewards, which SyncAll leaves to RewardHelper
                if (liveSum > 0 && (exactWrong > 0 || Math.Abs(liveSum - ourSum) > 2))
                {
                    report.Add(
                        $"{Path.GetFileName(account)} group {group.Id}: live {liveSum} ours {ourSum} of {members.Count}, exact members wrong {exactWrong}"
                    );
                }
            }
        }

        TestContext.Out.WriteLine(string.Join("\n", report));
        Assert.That(report, Is.Empty);
    }
}
