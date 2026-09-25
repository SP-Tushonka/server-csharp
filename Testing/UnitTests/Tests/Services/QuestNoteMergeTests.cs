using NUnit.Framework;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Services.InRaid;

namespace UnitTests.Tests.Services;

[TestFixture]
public class QuestNoteMergeTests
{
    private static readonly MongoId NoteFromRaid = new("68cc0f7ddabf984a73078e46");
    private static readonly MongoId NoteReadInMenu = new("68cbcfb02cdee534ad02d212");
    private static readonly MongoId QuestText = new("6895c24ec097ed522295a05d");

    [Test]
    public void MergeQuestNotes_NotesUnlockedInRaid_AreKept()
    {
        var server = new PmcData { QuestNotes = new() { { NoteReadInMenu, true } }, ReadQuestData = [] };
        var raid = new PmcData { QuestNotes = new() { { NoteFromRaid, false } }, ReadQuestData = [QuestText] };

        LocationLifecycleService.MergeQuestNotes(server, raid);

        Assert.That(server.QuestNotes, Is.EquivalentTo(new Dictionary<MongoId, bool> { { NoteReadInMenu, true }, { NoteFromRaid, false } }));
        Assert.That(server.ReadQuestData, Is.EquivalentTo(new[] { QuestText }));
    }

    [Test]
    public void MergeQuestNotes_NoteReadOnTheServerButUnreadInRaid_StaysRead()
    {
        var server = new PmcData { QuestNotes = new() { { NoteReadInMenu, true } } };
        var raid = new PmcData { QuestNotes = new() { { NoteReadInMenu, false } } };

        LocationLifecycleService.MergeQuestNotes(server, raid);

        Assert.That(server.QuestNotes![NoteReadInMenu], Is.True);
    }
}
