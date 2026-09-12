using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public class QuestVariableHelperTests
{
    // Ragman's first group, fully mapped from a live account
    private static readonly MongoId RagmanGroup = new("6a4b339f18db62e03b4f7ded");
    private static readonly MongoId Pathfinder = new("5ae449c386f7744bde357697");
    private static readonly MongoId PathfinderMember = new("6a4b32a22253e683e17702d8");

    private QuestVariableHelper _helper = default!;
    private TemplateTable _templates = default!;

    [SetUp]
    public void Setup()
    {
        _helper = DI.GetInstance().GetService<QuestVariableHelper>();
        _templates = DI.GetInstance().GetService<TemplateTable>();
    }

    [Test]
    public void RecordCompletion_ExactPair_SetsThatMemberAndReportsIt()
    {
        var pmc = ProfileWithCompleted(Pathfinder);
        var changes = new ProfileChange();

        _helper.RecordCompletion(pmc, Pathfinder, changes);

        Assert.That(pmc.Variables![PathfinderMember], Is.EqualTo(1));
        Assert.That(changes.VariableValues![PathfinderMember], Is.EqualTo(1));
    }

    [Test]
    public void RecordCompletion_Filler_TakesOneFreeMemberOnly()
    {
        var group = _templates.QuestVariables.Values.First(entry => entry.Fillers.Count > 0);
        var members = _templates.VariableGroups.First(entry => entry.Id == group.Id).Variables;
        var filler = group.Fillers[0].Quests[0];
        var pmc = ProfileWithCompleted(filler);

        _helper.RecordCompletion(pmc, filler, null);

        var set = members.Where(member => pmc.Variables!.GetValueOrDefault(member) != 0).ToList();
        Assert.That(set, Has.Count.EqualTo(1));
        Assert.That(group.Quests.Values, Does.Not.Contain(set[0]), "a filler must not take a member an exact pair owns");
    }

    [Test]
    public void SyncAll_RunTwice_ChangesNothingTheSecondTime()
    {
        var pmc = ProfileWithCompleted(Pathfinder, new MongoId("5ae448f286f77448d73c0131"));

        _helper.SyncAll(pmc);
        var first = new Dictionary<MongoId, int>(pmc.Variables!);
        _helper.SyncAll(pmc);

        Assert.That(pmc.Variables, Is.EqualTo(first));
        Assert.That(
            _templates.VariableGroups.First(entry => entry.Id == RagmanGroup).Variables.Sum(member => first.GetValueOrDefault(member)),
            Is.EqualTo(2)
        );
    }

    private static PmcData ProfileWithCompleted(params MongoId[] quests)
    {
        return new PmcData
        {
            Quests = quests
                .Select(quest => new QuestStatus
                {
                    QId = quest,
                    Status = QuestStatusEnum.Success,
                    StartTime = 0,
                    StatusTimers = [],
                })
                .ToList(),
            Variables = [],
        };
    }
}
