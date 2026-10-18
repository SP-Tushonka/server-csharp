using NUnit.Framework;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Server;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public class QuestChapterStartTests
{
    private static readonly MongoId TourQuest = new("6895bbb0e7dac53c7c08797b");
    private static readonly MongoId LabyrinthStarter = new("68e3a3abf1bccd83c50a5887");
    private static readonly MongoId LabyrinthChapter = new("68e3a35002661eb2d30ce387");
    private static readonly MongoId LabyrinthFirstTask = new("68e3a3bc02661eb2d30ce389");
    private static readonly MongoId PlayerTrader = new("67f7af56c117b6140af2a607");
    private static readonly MongoId BlueFireChapter = new("68e784b7fa3f1fa3770094ba");
    private static readonly MongoId BlueFireTalk = new("6a04b0b7d4b07b5ef1064e38");
    private static readonly MongoId BlueFireSell = new("67b892b832f50fb7ec084426");
    private static readonly MongoId BlueFireKeep = new("67b892ba0e9aad30720bfc56");
    private static readonly MongoId BlueFireAfterDeal = new("67b892bb1c013f0fcb058246");
    private static readonly MongoId SideHustle = new("6ac4c42976842fabb501d5b8");
    private static readonly MongoId Doxed = new("6ac4c726a3ea5ab42a074d3a");

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
    public void GetClientQuests_BlueFireTalkDone_StartsSellAndKeepTogether()
    {
        // The task between talking to Mechanic and the deal is missing from the database
        var sessionId = AddProfile(BlueFireTalk);
        AddQuest(sessionId, BlueFireChapter, QuestStatusEnum.Started);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Single(q => q.QId == BlueFireSell).Status, Is.EqualTo(QuestStatusEnum.Started), "sell");
        Assert.That(quests.Single(q => q.QId == BlueFireKeep).Status, Is.EqualTo(QuestStatusEnum.Started), "keep");
        Assert.That(quests.Any(q => q.QId == BlueFireAfterDeal), Is.False, "next task");
    }

    [TestCase(QuestStatusEnum.Success, QuestStatusEnum.Fail)]
    [TestCase(QuestStatusEnum.Fail, QuestStatusEnum.Success)]
    public void GetClientQuests_BlueFireEitherBranchTaken_StartsTheNextTask(QuestStatusEnum sell, QuestStatusEnum keep)
    {
        var sessionId = AddProfile(BlueFireTalk);
        AddQuest(sessionId, BlueFireChapter, QuestStatusEnum.Started);
        AddQuest(sessionId, BlueFireSell, sell);
        AddQuest(sessionId, BlueFireKeep, keep);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Single(q => q.QId == BlueFireAfterDeal).Status, Is.EqualTo(QuestStatusEnum.Started));
    }

    [Test]
    public void GetClientQuests_BlueFireLastTaskDoneAfterSelling_CompletesTheChapter()
    {
        // Keep failed and the two tasks the database lacks were never given, the client cannot complete this itself
        var sessionId = AddProfile(BlueFireTalk, BlueFireSell, BlueFireAfterDeal, new MongoId("67b892bd55eaf7b06d011436"));
        AddQuest(sessionId, BlueFireChapter, QuestStatusEnum.Started);
        AddQuest(sessionId, BlueFireKeep, QuestStatusEnum.Fail);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Single(q => q.QId == BlueFireChapter).Status, Is.EqualTo(QuestStatusEnum.Success));
    }

    [Test]
    public void CompleteQuest_BlueFireLastTask_ReportsTheChapterInTheSameResponse()
    {
        var postOffice = new MongoId("67b892bd55eaf7b06d011436");
        var sessionId = AddProfile(BlueFireTalk, BlueFireSell, BlueFireAfterDeal);
        AddQuest(sessionId, BlueFireChapter, QuestStatusEnum.Started);
        AddQuest(sessionId, BlueFireKeep, QuestStatusEnum.Fail);
        AddQuest(sessionId, postOffice, QuestStatusEnum.AvailableForFinish);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Bonuses = [];
        pmc.RepeatableQuests = [];

        var response = _questHelper.CompleteQuest(pmc, new CompleteQuestRequestData { QuestId = postOffice }, sessionId);

        var statuses = response.ProfileChanges[sessionId].QuestsStatus!;
        Assert.That(statuses.Any(q => q.QId == BlueFireChapter && q.Status == QuestStatusEnum.Success), Is.True);
    }

    [Test]
    public void GetClientQuests_BlueFireTaskStillRunning_KeepsTheChapterRunning()
    {
        var sessionId = AddProfile(BlueFireTalk, BlueFireSell);
        AddQuest(sessionId, BlueFireChapter, QuestStatusEnum.Started);
        AddQuest(sessionId, BlueFireKeep, QuestStatusEnum.Fail);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Single(q => q.QId == BlueFireAfterDeal).Status, Is.EqualTo(QuestStatusEnum.Started), "next task");
        Assert.That(quests.Single(q => q.QId == BlueFireChapter).Status, Is.EqualTo(QuestStatusEnum.Started), "chapter");
    }

    [Test]
    public void GetClientQuests_TaskWhoseFailConditionHolds_IsNotStarted()
    {
        // Boreas' filters hand in fails once the Reserve elimination task runs, so it belongs to the other branch
        var sessionId = AddProfile(
            new MongoId("69a05d3ae6778aebac085ca5"),
            new MongoId("69a9bc08babf5c053607fef7"),
            new MongoId("69aaecdf174ff467ef0acc38"),
            new MongoId("69aafbfa47e01d12cc087d0f"),
            new MongoId("69b2bb74ac1e735d5c0f9a57"),
            new MongoId("69b04c7ff4183ed66406e50c")
        );
        AddQuest(sessionId, new MongoId("69d38381cea4b428690ea1d9"), QuestStatusEnum.Started);
        AddQuest(sessionId, new MongoId("69b052349776545e5a02076a"), QuestStatusEnum.Started);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Any(q => q.QId == new MongoId("69b1ba3074141c146c03f9af")), Is.False);
    }

    [Test]
    public void GetClientQuests_BoreasTransportDialogue_StartsTheReserveTaskWhileTheTransportTaskRuns()
    {
        // Asking Prapor for transport sets 69b297fd, which starts the Reserve task before the tasks listed ahead of it
        var reserveTask = new MongoId("69b052349776545e5a02076a");
        var sessionId = AddProfile(
            new MongoId("69a05d3ae6778aebac085ca5"),
            new MongoId("69a9bc08babf5c053607fef7"),
            new MongoId("69aaecdf174ff467ef0acc38")
        );
        AddQuest(sessionId, new MongoId("69d38381cea4b428690ea1d9"), QuestStatusEnum.Started);
        AddQuest(sessionId, new MongoId("69aafbfa47e01d12cc087d0f"), QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;

        _questHelper.GetClientQuests(sessionId);
        Assert.That(pmc.Quests!.Any(q => q.QId == reserveTask), Is.False, "before the dialogue");

        pmc.Variables![new MongoId("69b297fd9fd8730942edc1f7")] = 1;
        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.Quests!.Single(q => q.QId == reserveTask).Status, Is.EqualTo(QuestStatusEnum.Started), "after the dialogue");
    }

    [TestCase(1, "69b052349776545e5a02076a")]
    [TestCase(2, "69b1ba3074141c146c03f9af")]
    [TestCase(3, "69b2b0cc0a7db6b331031267")]
    public void GetClientQuests_BoreasTransportChoice_StartsOnlyItsBranch(int choice, string branchTask)
    {
        // Prapor's transport line stores 1 for the Reserve task, 2 for the filters and 3 for the AMG-10 fluid
        string[] branches = ["69b052349776545e5a02076a", "69b1ba3074141c146c03f9af", "69b2b0cc0a7db6b331031267"];
        var sessionId = AddProfile(
            new MongoId("69a05d3ae6778aebac085ca5"),
            new MongoId("69a9bc08babf5c053607fef7"),
            new MongoId("69aaecdf174ff467ef0acc38"),
            new MongoId("69aafbfa47e01d12cc087d0f")
        );
        AddQuest(sessionId, new MongoId("69d38381cea4b428690ea1d9"), QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Variables![new MongoId("69b297fd9fd8730942edc1f7")] = choice;

        _questHelper.GetClientQuests(sessionId);

        foreach (var task in branches)
        {
            Assert.That(pmc.Quests!.Any(q => q.QId == new MongoId(task)), Is.EqualTo(task == branchTask), task);
        }
    }

    [TestCase(QuestStatusEnum.Started, "69b2c6beb8ae58006c084f73", "69b41843dc22acbf90051e3f")]
    [TestCase(QuestStatusEnum.Fail, "69b41843dc22acbf90051e3f", "69b2c6beb8ae58006c084f73")]
    public void GetClientQuests_BoreasAlternativeTransportDone_StartsTheBtrBranch(QuestStatusEnum helper, string started, string skipped)
    {
        // The hidden helper fails once The Price of Independence is done, which moves the BTR driver's job
        var sessionId = AddProfile(
            new MongoId("69a05d3ae6778aebac085ca5"),
            new MongoId("69a9bc08babf5c053607fef7"),
            new MongoId("69aaecdf174ff467ef0acc38"),
            new MongoId("69aafbfa47e01d12cc087d0f"),
            new MongoId("69b04c7ff4183ed66406e50c")
        );
        AddQuest(sessionId, new MongoId("69d38381cea4b428690ea1d9"), QuestStatusEnum.Started);
        AddQuest(sessionId, new MongoId("69fb04933bca906c71052ddc"), helper);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.TradersInfo![new MongoId("656f0f98d80a697f855d34b1")] = new TraderInfo
        {
            LoyaltyLevel = 1,
            Standing = 0,
            Unlocked = true,
        };

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.Quests!.Single(q => q.QId == new MongoId(started)).Status, Is.EqualTo(QuestStatusEnum.Started), "started");
        Assert.That(pmc.Quests!.Any(q => q.QId == new MongoId(skipped)), Is.False, "skipped");
    }

    [TestCase(3600, false)]
    [TestCase(-3600, true)]
    public void GetClientQuests_QuestOnUnlockTimer_ListedOnceTheTimerRunsOut(int secondsLeft, bool listed)
    {
        var bitcoinsTask = new MongoId("67bc9eda1d99d6ebe5014b65");
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Quests!.Add(
            new QuestStatus
            {
                QId = bitcoinsTask,
                StartTime = 0,
                Status = QuestStatusEnum.AvailableAfter,
                StatusTimers = [],
                AvailableAfter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + secondsLeft,
            }
        );

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == bitcoinsTask), Is.EqualTo(listed));
    }

    [Test]
    public void GetClientQuests_FinishedChapter_StartsNoGatedTask()
    {
        var sessionId = AddProfile(new MongoId("69aafbfa47e01d12cc087d0f"));
        AddQuest(sessionId, new MongoId("69d38381cea4b428690ea1d9"), QuestStatusEnum.Success);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Variables![new MongoId("69b297fd9fd8730942edc1f7")] = 1;

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.Quests!.Any(q => q.QId == new MongoId("69b052349776545e5a02076a")), Is.False);
    }

    [TestCase("69a05d3ae6778aebac085ca5")]
    [TestCase("69aae3aa97c076d9b503bceb")]
    public void GetClientQuests_EitherBoreasEntryDone_StartsTheCellTowerTask(string entryTask)
    {
        // Boreas opens through one of two tasks other quests start, then the cell tower once the missing task between is skipped
        var boreasChapter = new MongoId("69d38381cea4b428690ea1d9");
        var cellTower = new MongoId("69a9bc08babf5c053607fef7");
        var sessionId = AddProfile(new MongoId(entryTask));
        AddQuest(sessionId, boreasChapter, QuestStatusEnum.Started);

        _questHelper.GetClientQuests(sessionId);

        var quests = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Quests!;
        Assert.That(quests.Single(q => q.QId == cellTower).Status, Is.EqualTo(QuestStatusEnum.Started));
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

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    public void GetClientQuests_NewBeginning_OfferedOnlyAtItsPrestigeLevel(int prestigeLevel, bool offered)
    {
        // The first New Beginning asks for level 20 and prestige level 1
        var newBeginning = new MongoId("6aad73ddedf6e9696e0f60d4");
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Info!.Level = 20;
        pmc.Info.SetPrestigeLevel(PrestigeGameModes.Pve, prestigeLevel);
        pmc.TradersInfo![Traders.RAGMAN] = new TraderInfo
        {
            LoyaltyLevel = 1,
            Standing = 0,
            Unlocked = true,
        };

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == newBeginning), Is.EqualTo(offered));
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

    [TestCase("6ac4c42976842fabb501d5b8", "6ac4c726a3ea5ab42a074d3a")]
    [TestCase("6752f6d83038f7df520c83e8", "673f348dd3346c21670217e7")]
    public void StartQuestsStartedWith_PartnerAccepted_StartsTheQuest(string accepted, string expected)
    {
        DI.GetInstance().GetService<SeasonalEventService>().ForceSeasonalEvent(SeasonalEventType.Scourge);
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        AddQuest(sessionId, new MongoId(accepted), QuestStatusEnum.Started);

        var started = _questHelper.StartQuestsStartedWith(pmc, new MongoId(accepted));

        Assert.That(started.Single().Id, Is.EqualTo(new MongoId(expected)));
        Assert.That(started.Single().Status, Is.EqualTo(QuestStatusEnum.Started));
        Assert.That(pmc.GetQuestStatus(new MongoId(expected)), Is.EqualTo(QuestStatusEnum.Started));
        Assert.That(_questHelper.GetNewlyAccessibleQuestsWhenStartingQuest(new MongoId(accepted), sessionId).Any(q => q.Id == new MongoId(expected)), Is.False);
    }

    [TestCase(60, false)]
    [TestCase(21600, true)]
    public void GetClientQuests_RouteDeviation_OfferedSixHoursAfterTheHigherTheyFly(int secondsAgo, bool offered)
    {
        var theHigherTheyFly = new MongoId("6745fae369a58fceba10343d");
        var routeDeviation = new MongoId("674600a366e6a521aa05eb66");
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Info!.Level = 40;
        pmc.TradersInfo![Traders.SKIER] = new TraderInfo
        {
            LoyaltyLevel = 4,
            Standing = 1,
            Unlocked = true,
        };
        pmc.Quests!.Add(
            new QuestStatus
            {
                QId = theHigherTheyFly,
                StartTime = 0,
                Status = QuestStatusEnum.Success,
                StatusTimers = new Dictionary<QuestStatusEnum, double>
                {
                    { QuestStatusEnum.Success, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - secondsAgo },
                },
            }
        );

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == routeDeviation), Is.EqualTo(offered));
    }

    [Test]
    public void GetClientQuests_Doxed_HiddenUntilSideHustleIsAccepted()
    {
        DI.GetInstance().GetService<SeasonalEventService>().ForceSeasonalEvent(SeasonalEventType.Scourge);
        var sessionId = AddProfile();

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == Doxed), Is.False, "before");

        AddQuest(sessionId, SideHustle, QuestStatusEnum.Started);
        var doxed = _questHelper.GetClientQuests(sessionId).SingleOrDefault(q => q.Id == Doxed);

        Assert.That(doxed?.SptStatus, Is.EqualTo(QuestStatusEnum.Started), "after");
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

    private void AddQuest(MongoId sessionId, MongoId questId, QuestStatusEnum status)
    {
        _saveServer
            .GetProfile(sessionId)
            .CharacterData!.PmcData!.Quests!.Add(
                new QuestStatus
                {
                    QId = questId,
                    StartTime = 0,
                    Status = status,
                    StatusTimers = new Dictionary<QuestStatusEnum, double> { { status, 0 } },
                }
            );
    }
}
