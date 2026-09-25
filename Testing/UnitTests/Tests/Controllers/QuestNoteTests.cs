using NUnit.Framework;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Servers;

namespace UnitTests.Tests.Controllers;

[TestFixture]
public class QuestNoteTests
{
    private static readonly MongoId Note = new("68cbcfb02cdee534ad02d212");
    private static readonly MongoId DayThirtyFiveTape = new("68889aca77aeb067290816f1");

    private QuestController _controller = default!;

    [SetUp]
    public void Setup()
    {
        _controller = DI.GetInstance().GetService<QuestController>();
    }

    [Test]
    public void AddQuestNote_NewNote_IsStoredUnread()
    {
        var (pmc, sessionId) = Profile();

        _controller.AddQuestNote(pmc, new AddQuestNoteRequest { NoteId = Note }, sessionId);

        Assert.That(pmc.QuestNotes![Note], Is.False);
    }

    [Test]
    public void ReadQuestNote_AddedNote_IsMarkedRead()
    {
        var (pmc, sessionId) = Profile();
        _controller.AddQuestNote(pmc, new AddQuestNoteRequest { NoteId = Note }, sessionId);

        _controller.ReadQuestNote(pmc, new ReadQuestNoteRequest { NoteId = Note }, sessionId);

        Assert.That(pmc.QuestNotes![Note], Is.True);
    }

    [Test]
    public void AddQuestNote_AlreadyRead_StaysRead()
    {
        var (pmc, sessionId) = Profile();
        _controller.ReadQuestNote(pmc, new ReadQuestNoteRequest { NoteId = Note }, sessionId);

        _controller.AddQuestNote(pmc, new AddQuestNoteRequest { NoteId = Note }, sessionId);

        Assert.That(pmc.QuestNotes![Note], Is.True);
    }

    [Test]
    public void CompleteItem_TapeReadInStash_JoinsTheCollection()
    {
        var (pmc, sessionId) = Profile();

        _controller.CompleteItem(pmc, new CompleteItemRequest { CompletableItemId = DayThirtyFiveTape }, sessionId);

        Assert.That(pmc.CompletableItems![DayThirtyFiveTape], Is.True);
    }

    private static (PmcData Pmc, MongoId SessionId) Profile()
    {
        var sessionId = new MongoId();
        var pmc = new PmcData
        {
            Id = sessionId,
            Info = new SPTarkov.Server.Core.Models.Eft.Common.Tables.Info { Experience = 0 },
        };
        DI.GetInstance()
            .GetService<SaveServer>()
            .AddProfile(
                new SptProfile
                {
                    ProfileInfo = new SPTarkov.Server.Core.Models.Eft.Profile.Info { ProfileId = sessionId },
                    CharacterData = new Characters { PmcData = pmc },
                }
            );

        return (pmc, sessionId);
    }
}
