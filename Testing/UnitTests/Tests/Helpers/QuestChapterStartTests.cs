using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Servers;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public class QuestChapterStartTests
{
    private static readonly MongoId TourQuest = new("6895bbb0e7dac53c7c08797b");
    private static readonly MongoId LabyrinthStarter = new("68e3a3abf1bccd83c50a5887");
    private static readonly MongoId LabyrinthChapter = new("68e3a35002661eb2d30ce387");
    private static readonly MongoId LabyrinthFirstTask = new("68e3a3bc02661eb2d30ce389");
    private static readonly MongoId PlayerTrader = new("67f7af56c117b6140af2a607");

    private QuestHelper _questHelper = default!;
    private SaveServer _saveServer = default!;

    [SetUp]
    public void Setup()
    {
        _questHelper = DI.GetInstance().GetService<QuestHelper>();
        _saveServer = DI.GetInstance().GetService<SaveServer>();
    }

    [Test]
    public void GetClientQuests_TourDone_StartsTheLabyrinthVisitTaskButNotTheChapter()
    {
        var sessionId = AddProfile(TourQuest);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Any(q => q.QId == LabyrinthStarter && q.Status == QuestStatusEnum.Started), Is.True);
        Assert.That(quests.Any(q => q.QId == LabyrinthChapter), Is.False);
    }

    [Test]
    public void GetClientQuests_VisitTaskDone_StartsTheChapterAndItsFirstTask()
    {
        var sessionId = AddProfile(TourQuest, LabyrinthStarter);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Any(q => q.QId == LabyrinthChapter && q.Status == QuestStatusEnum.Started), Is.True, "chapter");
        Assert.That(quests.Any(q => q.QId == LabyrinthFirstTask && q.Status == QuestStatusEnum.Started), Is.True, "first task");
    }

    [Test]
    public void GetClientQuests_TaskStartedInRaid_StartsItsChapter()
    {
        // Blue Fire's first task is started by the client when its note is read in raid
        var blueFireChapter = new MongoId("68e784b7fa3f1fa3770094ba");
        var blueFireFirstTask = new MongoId("67b892b338076c36e50abfb5");
        var sessionId = AddProfile();
        _saveServer
            .GetProfile(sessionId)
            .CharacterData!.PmcData!.Quests!.Add(
                new QuestStatus
                {
                    QId = blueFireFirstTask,
                    StartTime = 0,
                    Status = QuestStatusEnum.Started,
                    StatusTimers = new Dictionary<QuestStatusEnum, double> { { QuestStatusEnum.Started, 0 } },
                }
            );

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Any(q => q.QId == blueFireChapter && q.Status == QuestStatusEnum.Started), Is.True);
    }

    [Test]
    public void GetClientQuests_TaskAcceptedInDialogue_WaitsForThePlayer()
    {
        // Falling Skies task handed out by a Prapor dialogue line, unlocked by 678f6bd1
        var prerequisite = new MongoId("678f6bd1e8d46e40ff021605");
        var dialogueTask = new MongoId("678f9df57252f8f0ae02f2d6");
        var sessionId = AddProfile(prerequisite);

        var shown = _questHelper.GetClientQuests(sessionId);

        Assert.That(shown.Single(q => q.Id == dialogueTask).SptStatus, Is.EqualTo(QuestStatusEnum.AvailableForStart));
        Assert.That(_saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!.Any(q => q.QId == dialogueTask), Is.False);
    }

    private MongoId AddProfile(params MongoId[] completed)
    {
        var sessionId = new MongoId();
        var pmc = new PmcData
        {
            Info = new SPTarkov.Server.Core.Models.Eft.Common.Tables.Info
            {
                Side = "Usec",
                Level = 5,
                GameVersion = GameEditions.STANDARD,
            },
            Quests = completed
                .Select(quest => new QuestStatus
                {
                    QId = quest,
                    StartTime = 0,
                    Status = QuestStatusEnum.Success,
                    StatusTimers = new Dictionary<QuestStatusEnum, double> { { QuestStatusEnum.Success, 0 } },
                })
                .ToList(),
            TradersInfo = new Dictionary<MongoId, TraderInfo>
            {
                {
                    PlayerTrader,
                    new TraderInfo
                    {
                        LoyaltyLevel = 1,
                        Standing = 0,
                        Unlocked = true,
                    }
                },
                {
                    Traders.PRAPOR,
                    new TraderInfo
                    {
                        LoyaltyLevel = 1,
                        Standing = 0,
                        Unlocked = true,
                    }
                },
                {
                    Traders.MECHANIC,
                    new TraderInfo
                    {
                        LoyaltyLevel = 1,
                        Standing = 0,
                        Unlocked = true,
                    }
                },
            },
            Variables = [],
        };

        _saveServer.AddProfile(
            new SptProfile
            {
                ProfileInfo = new SPTarkov.Server.Core.Models.Eft.Profile.Info { ProfileId = sessionId },
                CharacterData = new Characters { PmcData = pmc },
            }
        );

        return sessionId;
    }
}
