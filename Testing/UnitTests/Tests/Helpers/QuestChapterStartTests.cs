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
    public void GetClientQuests_PreviousChapterTaskDone_StartsTheUngatedTask()
    {
        // Falling Skies' second task has no start conditions, live starts it the moment the first task is done
        var fallingSkiesStarter = new MongoId("6914f60df06f0ee753006191");
        var firstTask = new MongoId("68dfa85efdedf14d640a6ee0");
        var praporTask = new MongoId("678f6bd1e8d46e40ff021605");

        var shown = _questHelper.GetClientQuests(AddProfile(fallingSkiesStarter));
        Assert.That(shown.Any(q => q.Id == praporTask), Is.False, "first task open");

        shown = _questHelper.GetClientQuests(AddProfile(fallingSkiesStarter, firstTask));
        Assert.That(shown.Single(q => q.Id == praporTask).SptStatus, Is.EqualTo(QuestStatusEnum.Started), "first task done");
    }

    [Test]
    public void GetClientQuests_RecordWrittenBackAsAvailableForStart_IsResolvedAgain()
    {
        // A raid save carries the client's AvailableForStart record for a task the server has since unlocked
        var fallingSkiesStarter = new MongoId("6914f60df06f0ee753006191");
        var firstTask = new MongoId("68dfa85efdedf14d640a6ee0");
        var praporTask = new MongoId("678f6bd1e8d46e40ff021605");
        var sessionId = AddProfile(fallingSkiesStarter, firstTask);
        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        quests.Add(
            new QuestStatus
            {
                QId = praporTask,
                StartTime = 0,
                Status = QuestStatusEnum.AvailableForStart,
                StatusTimers = [],
            }
        );

        var shown = _questHelper.GetClientQuests(sessionId);

        Assert.That(shown.Single(q => q.Id == praporTask).SptStatus, Is.EqualTo(QuestStatusEnum.Started));
        Assert.That(quests.Single(q => q.QId == praporTask).Status, Is.EqualTo(QuestStatusEnum.Started));
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

    [Test]
    public void GetClientQuests_WithheldQuest_IsNotOffered()
    {
        // Green Corridor ships without start conditions but live never offers it to a fresh account
        var greenCorridor = new MongoId("639136d68ba6894d155e77cf");
        var sessionId = AddProfile();

        var shown = _questHelper.GetClientQuests(sessionId);

        Assert.That(shown.Any(q => q.Id == greenCorridor), Is.False);
    }

    [Test]
    public void GetClientQuests_HiddenGate_OpensAtTheGroupValue()
    {
        // Big Game ships without start conditions, live offers it once four Jaeger LL1 flags are set
        var bigGame = new MongoId("64e7b971f9d6fa49d6769b44");
        var jaegerMembers = new[]
        {
            "6a42a5bb96edcaff82c441c4",
            "6a42a5d2898c034ebfa35d07",
            "6a42a5e6c375b2db1ac867a5",
            "6a42a6075ed52695759e8e66",
        };
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.TradersInfo![Traders.JAEGER] = new TraderInfo
        {
            LoyaltyLevel = 1,
            Standing = 0,
            Unlocked = true,
        };
        var variables = pmc.Variables!;
        foreach (var member in jaegerMembers.Take(3))
        {
            variables[new MongoId(member)] = 1;
        }

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == bigGame), Is.False, "three flags");

        variables[new MongoId(jaegerMembers[3])] = 1;

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == bigGame), Is.True, "four flags");
    }

    [Test]
    public void GetClientQuests_HiddenGate_OpensAtTheTraderStanding()
    {
        // Health Care Privacy - Part 1 ships without start conditions, live offers it at Therapist standing 0.5
        var healthCarePrivacy = new MongoId("5a68661a86f774500f48afb0");
        var therapist = new MongoId("54cb57776803fa99248b456e");
        var sessionId = AddProfile();
        var traders = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.TradersInfo!;
        if (!traders.ContainsKey(therapist))
        {
            traders[therapist] = new TraderInfo { Unlocked = true };
        }

        traders[therapist].Standing = 0.4;

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == healthCarePrivacy), Is.False, "standing 0.4");

        traders[therapist].Standing = 0.5;

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == healthCarePrivacy), Is.True, "standing 0.5");
    }

    [Test]
    public void GetClientQuests_HiddenGate_OpensAtTheTraderLoyalty()
    {
        var mechanicQuest = new MongoId("68dbf539675bd8efd403ec10");
        var sessionId = AddProfile();
        var mechanic = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.TradersInfo![Traders.MECHANIC];

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == mechanicQuest), Is.False, "loyalty 1");

        mechanic.LoyaltyLevel = 4;

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == mechanicQuest), Is.True, "loyalty 4");
    }

    [Test]
    public void GetClientQuests_HiddenGate_OpensAfterThePrerequisiteQuest()
    {
        // Breathing Room ships without start conditions, live still asks for Friend from Norvinsk - Part 5
        var breathingRoom = new MongoId("6864fcef9809a149400dd2ee");
        var friendFromNorvinsk5 = new MongoId("686404d348e7bb4146002cac");

        Assert.That(_questHelper.GetClientQuests(AddProfile()).Any(q => q.Id == breathingRoom), Is.False, "without");
        Assert.That(_questHelper.GetClientQuests(AddProfile(friendFromNorvinsk5)).Any(q => q.Id == breathingRoom), Is.True, "with");
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
                {
                    Traders.THERAPIST,
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
