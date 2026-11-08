using NUnit.Framework;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Migration.Migrations._5._0;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils;

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
    private RepairStoryChapters _repairStoryChapters = default!;
    private SaveServer _saveServer = default!;

    [SetUp]
    public void Setup()
    {
        _questHelper = DI.GetInstance().GetService<QuestHelper>();
        _repairStoryChapters = new RepairStoryChapters(
            DI.GetInstance().GetService<MainStoryChapterHelper>(),
            DI.GetInstance().GetService<JsonUtil>(),
            DI.GetInstance().GetService<TimeUtil>()
        );
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

    [TestCase(1, "69b052349776545e5a02076a")]
    [TestCase(2, "69b1ba3074141c146c03f9af")]
    public void GetClientQuests_BoreasReserveOrFiltersDone_StartsTheFluidTask(int choice, string branchTask)
    {
        var fluidTask = new MongoId("69f051ae0a5238af8b00e11c");
        var sessionId = AddProfile(
            new MongoId("69a05d3ae6778aebac085ca5"),
            new MongoId("69a9bc08babf5c053607fef7"),
            new MongoId("69aaecdf174ff467ef0acc38"),
            new MongoId("69aafbfa47e01d12cc087d0f"),
            new MongoId(branchTask)
        );
        AddQuest(sessionId, new MongoId("69d38381cea4b428690ea1d9"), QuestStatusEnum.Started);
        AddQuest(sessionId, new MongoId("69b04c7ff4183ed66406e50c"), QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Variables![new MongoId("69b297fd9fd8730942edc1f7")] = choice;

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.Quests!.Single(q => q.QId == fluidTask).Status, Is.EqualTo(QuestStatusEnum.Started));
    }

    [TestCase("68cbcdc4c964ab83cc0c928e", "68c00c0ee726287ae60edd72", "678f6bd1e8d46e40ff021605")]
    [TestCase("68cbd33676fe74b1e80bfd91", "6909e9db9ce51fc8cd014c6e", "68c6aa0e2f0a1d31380ad618")]
    [TestCase("68e3a35002661eb2d30ce387", "68e3a3d2ff05916c250a898b", "68ffdaf2853a782081039a16")]
    [TestCase("6900927ab7d28358f80b9421", "67ced01a39a6a1807f093ec5", "67ced01df816f16d9a00d586")]
    public void GetClientQuests_TaskWithSecondRoute_StartsWithoutTheTaskListedBeforeIt(string chapter, string prerequisite, string task)
    {
        // Falling Skies after the older plane quest, the Tour drainage task after the Labs keycard, Labyrinth's last task
        // after #3 and The Unheard's hidden room after #13
        var sessionId = AddProfile(new MongoId(prerequisite));
        AddQuest(sessionId, new MongoId(chapter), QuestStatusEnum.Started);

        _questHelper.GetClientQuests(sessionId);

        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        Assert.That(pmc.Quests!.Single(q => q.QId == new MongoId(task)).Status, Is.EqualTo(QuestStatusEnum.Started));
    }

    [TestCase(QuestStatusEnum.Started, QuestStatusEnum.Started, "69b2c6beb8ae58006c084f73")]
    [TestCase(QuestStatusEnum.Fail, QuestStatusEnum.Started, "69b41843dc22acbf90051e3f")]
    [TestCase(QuestStatusEnum.Started, QuestStatusEnum.Fail, "69b3e0d4eeea23ab5601d62d")]
    public void GetClientQuests_BoreasAlternativeTransportDone_StartsTheBranchOfTheSideTaken(
        QuestStatusEnum btrHelper,
        QuestStatusEnum skierHelper,
        string started
    )
    {
        // Each hidden helper fails once its side's quest is done, The Price of Independence or Choose Your Friends Wisely
        string[] branches = ["69b41843dc22acbf90051e3f", "69b2c6beb8ae58006c084f73", "69b3e0d4eeea23ab5601d62d"];
        var sessionId = AddProfile(
            new MongoId("69a05d3ae6778aebac085ca5"),
            new MongoId("69a9bc08babf5c053607fef7"),
            new MongoId("69aaecdf174ff467ef0acc38"),
            new MongoId("69aafbfa47e01d12cc087d0f"),
            new MongoId("69b04c7ff4183ed66406e50c")
        );
        AddQuest(sessionId, new MongoId("69d38381cea4b428690ea1d9"), QuestStatusEnum.Started);
        AddQuest(sessionId, new MongoId("69fb04933bca906c71052ddc"), btrHelper);
        AddQuest(sessionId, new MongoId("69eb680847f27502e600de57"), skierHelper);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.TradersInfo![new MongoId("656f0f98d80a697f855d34b1")] = new TraderInfo
        {
            LoyaltyLevel = 1,
            Standing = 0,
            Unlocked = true,
        };

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.Quests!.Single(q => q.QId == new MongoId(started)).Status, Is.EqualTo(QuestStatusEnum.Started), "started");
        foreach (var task in branches.Where(task => task != started))
        {
            Assert.That(pmc.Quests!.Any(q => q.QId == new MongoId(task)), Is.False, task);
        }
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
        var greenCorridor = new MongoId("639136d68ba6894d155e77cf");
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Info!.Side = "Bear";

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == greenCorridor), Is.False, "loyalty 1");

        pmc.TradersInfo![Traders.PRAPOR].LoyaltyLevel = 2;

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == greenCorridor), Is.True, "loyalty 2");
    }

    [Test]
    public void GetClientQuests_ArmoredCaseCraftQuest_OfferedWhileMechanicsQuestRuns()
    {
        // Mechanic only offers the line that accepts this quest while the quest is listed as available
        var craftQuest = new MongoId("68dbf539675bd8efd403ec10");
        var mechanicQuest = new MongoId("67bc86024e7dbc3aa202431b");
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.TradersInfo![Traders.MECHANIC].LoyaltyLevel = 4;

        Assert.That(_questHelper.GetClientQuests(sessionId).Any(q => q.Id == craftQuest), Is.False, "before Mechanic's quest");

        AddQuest(sessionId, mechanicQuest, QuestStatusEnum.Started);

        Assert.That(_questHelper.GetClientQuests(sessionId).Single(q => q.Id == craftQuest).SptStatus, Is.EqualTo(QuestStatusEnum.AvailableForStart));
    }

    [TestCase("656f0f98d80a697f855d34b1", "69c26c07683c9831020018c7", "69c27158e350f01390049e77", QuestStatusEnum.Started)]
    [TestCase("5a7c2eca46aef81a7ca2145d", "6a880f5d9438980c42076efe", "6a88270efbd24b6243010e6d", QuestStatusEnum.Started)]
    [TestCase("5a7c2eca46aef81a7ca2145d", "6a88270efbd24b6243010e6d", "6a8814d20d25ca7b2908e13e", QuestStatusEnum.AvailableForStart)]
    public void GetClientQuests_UnlockedQuest_StartsOnlyWhenConfigured(string trader, string prerequisite, string quest, QuestStatusEnum expected)
    {
        var sessionId = AddProfile(new MongoId(prerequisite));
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.TradersInfo![new MongoId(trader)] = new TraderInfo
        {
            LoyaltyLevel = 1,
            Standing = 0,
            Unlocked = true,
        };

        Assert.That(_questHelper.GetClientQuests(sessionId).Single(q => q.Id == new MongoId(quest)).SptStatus, Is.EqualTo(expected));
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

        var started = DI.GetInstance().GetService<StoryProgressHelper>().StartQuestsStartedWith(pmc, new MongoId(accepted));

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

    [Test]
    public void GetClientQuests_NextTaskWaitsForDialogue_KeepsTheChapterRunning()
    {
        // Blue Fire's hacking device task also needs Mechanic's dialogue variable 6a04f439 at 2
        var sessionId = AddProfile(new MongoId("67b892b338076c36e50abfb5"), new MongoId("67b892b4884d39aaea04ef26"));
        AddQuest(sessionId, BlueFireChapter, QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;

        _questHelper.GetClientQuests(sessionId);
        Assert.That(pmc.GetQuestStatus(BlueFireChapter), Is.EqualTo(QuestStatusEnum.Started), "before the dialogue");

        pmc.Variables![new MongoId("6a04f439a20c93e80120d5c9")] = 2;
        _questHelper.GetClientQuests(sessionId);
        Assert.That(pmc.GetQuestStatus(new MongoId("67b892b6b4c09aae5309c275")), Is.EqualTo(QuestStatusEnum.Started), "after the dialogue");
    }

    [Test]
    public void GetClientQuests_NextTaskOnUnlockTimer_KeepsTheChapterRunning()
    {
        // The Labyrinth's BEAR squad task unlocks 16 hours after the first task
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var task in new[] { "68e3a3bc02661eb2d30ce389", "68e4cf5136a9240a3704e777" })
        {
            pmc.Quests!.Add(
                new QuestStatus
                {
                    QId = new MongoId(task),
                    StartTime = now,
                    Status = QuestStatusEnum.Success,
                    StatusTimers = new Dictionary<QuestStatusEnum, double> { { QuestStatusEnum.Success, now } },
                }
            );
        }

        AddQuest(sessionId, LabyrinthChapter, QuestStatusEnum.Started);

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.GetQuestStatus(LabyrinthChapter), Is.EqualTo(QuestStatusEnum.Started));
    }

    [Test]
    public void GetClientQuests_NextTaskWaitsForHiddenStep_KeepsTheChapterRunning()
    {
        // Boreas #7 starts once the hidden transport step finishes, which happens after both transport branches
        var boreas = new MongoId("69d38381cea4b428690ea1d9");
        var sessionId = AddProfile(
            new MongoId("69a05d3ae6778aebac085ca5"),
            new MongoId("69a9bc08babf5c053607fef7"),
            new MongoId("69aaecdf174ff467ef0acc38"),
            new MongoId("69aafbfa47e01d12cc087d0f"),
            new MongoId("69b04c7ff4183ed66406e50c"),
            new MongoId("69b052349776545e5a02076a"),
            new MongoId("69f051ae0a5238af8b00e11c"),
            new MongoId("69b2c6beb8ae58006c084f73")
        );
        AddQuest(sessionId, boreas, QuestStatusEnum.Started);
        AddQuest(sessionId, new MongoId("69b04f2f4ec955a5f40828a3"), QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Variables![new MongoId("69b297fd9fd8730942edc1f7")] = 1;

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.GetQuestStatus(boreas), Is.EqualTo(QuestStatusEnum.Started));
    }

    [Test]
    public void CompleteQuest_BlueFireFragmentKept_FailsTheRunningHandOver()
    {
        var sessionId = AddProfile(new MongoId("67b892b338076c36e50abfb5"), new MongoId("67b892b4884d39aaea04ef26"), BlueFireTalk);
        AddQuest(sessionId, BlueFireChapter, QuestStatusEnum.Started);
        AddQuest(sessionId, BlueFireSell, QuestStatusEnum.Started);
        AddQuest(sessionId, BlueFireKeep, QuestStatusEnum.AvailableForFinish);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Bonuses = [];
        pmc.RepeatableQuests = [];
        pmc.Achievements = [];

        _questHelper.CompleteQuest(pmc, new CompleteQuestRequestData { QuestId = BlueFireKeep }, sessionId);

        Assert.That(pmc.GetQuestStatus(BlueFireSell), Is.EqualTo(QuestStatusEnum.Fail));
    }

    [Test]
    public void GetClientQuests_UnheardWaitForElektronik_OffersTheTechFilesTask()
    {
        // The wait ends when the tech files task starts, and Elektronik's line only shows while that task is on offer
        var techFiles = new MongoId("67ced01a39a6a1807f093ec5");
        var sessionId = AddProfile(
            new MongoId("67ced00eceefc356a10fd405"),
            new MongoId("67ced00fd47d32692302aca6"),
            new MongoId("67ced012152372f12708b7b6"),
            new MongoId("68ec64b49012da9779025825"),
            new MongoId("67ced014c43709665f0fbe46"),
            new MongoId("67ced0157844ea90d80fd6a5"),
            new MongoId("67ced017029b9dbe190f3c77"),
            new MongoId("67ced018d1e611aa410235e5")
        );
        AddQuest(sessionId, new MongoId("6900927ab7d28358f80b9421"), QuestStatusEnum.Started);
        AddQuest(sessionId, new MongoId("68ee523e54da3fa9aa01af75"), QuestStatusEnum.Started);
        _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.TradersInfo![Traders.KERMAN] = new TraderInfo
        {
            LoyaltyLevel = 1,
            Standing = 0,
            Unlocked = true,
        };

        var shown = _questHelper.GetClientQuests(sessionId);

        Assert.That(shown.Single(q => q.Id == techFiles).SptStatus, Is.EqualTo(QuestStatusEnum.AvailableForStart));
    }

    [TestCase("68e784b7fa3f1fa3770094ba", new[] { "67b892b338076c36e50abfb5", "67b892b4884d39aaea04ef26" }, null)]
    [TestCase(
        "6903d779fdfc4078740a4bd0",
        new[]
        {
            "68e79a92aded99fd1a06023c", "68588ec88b0cb7b6f80f9339", "68588eca1d2ea426e9085c29", "68588ecc10ea119db4095de7",
            "68588ed156fb54c7e00e4318", "68588ed2b2a45312020df028", "68588ed4257202ddee073e07", "68588ed5c7a9b3b530005ec8",
            "68588ed9f981006b1e0a7718", "690413d50750895a590a3bbd", "68588edb1dbd9790be0f5ee9", "6914d59b1bd36f988e0d95bc",
            "68588edc157cc3165608f2b7",
        },
        null
    )]
    [TestCase(
        "6900927ab7d28358f80b9421",
        new[]
        {
            "67ced00eceefc356a10fd405", "67ced00fd47d32692302aca6", "67ced012152372f12708b7b6", "68ec64b49012da9779025825",
            "67ced014c43709665f0fbe46", "67ced0157844ea90d80fd6a5", "67ced017029b9dbe190f3c77", "67ced018d1e611aa410235e5",
        },
        "68ee523e54da3fa9aa01af75"
    )]
    [TestCase(
        "69d38381cea4b428690ea1d9",
        new[]
        {
            "69a05d3ae6778aebac085ca5", "69a9bc08babf5c053607fef7", "69aaecdf174ff467ef0acc38", "69aafbfa47e01d12cc087d0f",
            "69b04c7ff4183ed66406e50c", "69b052349776545e5a02076a", "69f051ae0a5238af8b00e11c", "69b2c6beb8ae58006c084f73",
        },
        "69b04f2f4ec955a5f40828a3"
    )]
    public void RepairStoryChapters_ChapterClosedWhileATaskWasComing_ReopensIt(string chapter, string[] done, string? running)
    {
        // Blue Fire waiting on Mechanic's dialogue, They Are Already Here on the specs dialogue, The Unheard on the wait
        // for Elektronik and Boreas on the hidden transport step
        var sessionId = AddProfile(done.Select(id => new MongoId(id)).ToArray());
        AddQuest(sessionId, new MongoId(chapter), QuestStatusEnum.Success);
        if (running is not null)
        {
            AddQuest(sessionId, new MongoId(running), QuestStatusEnum.Started);
        }

        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Variables![new MongoId("69b297fd9fd8730942edc1f7")] = 1;

        _repairStoryChapters.Repair(pmc);

        Assert.That(pmc.GetQuestStatus(new MongoId(chapter)), Is.EqualTo(QuestStatusEnum.Started));
    }

    [Test]
    public void RepairStoryChapters_ChapterClosedDuringAnUnlockTimer_ReopensIt()
    {
        var sessionId = AddProfile();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var task in new[] { "68e3a3bc02661eb2d30ce389", "68e4cf5136a9240a3704e777" })
        {
            pmc.Quests!.Add(
                new QuestStatus
                {
                    QId = new MongoId(task),
                    StartTime = now,
                    Status = QuestStatusEnum.Success,
                    StatusTimers = new Dictionary<QuestStatusEnum, double> { { QuestStatusEnum.Success, now } },
                }
            );
        }

        AddQuest(sessionId, LabyrinthChapter, QuestStatusEnum.Success);

        _repairStoryChapters.Repair(pmc);

        Assert.That(pmc.GetQuestStatus(LabyrinthChapter), Is.EqualTo(QuestStatusEnum.Started));
    }

    [TestCase("67b892ba0e9aad30720bfc56", "67b892b832f50fb7ec084426")]
    [TestCase("68cd7d6d9510d63fdb05a76a", "679cdee4ce3a208fee0ad657")]
    public void RepairStoryChapters_AlternativeLeftRunning_FailsIt(string chosen, string alternative)
    {
        // Keeping Blue Fire's fragment or Falling Skies' armored case fails the hand over
        var sessionId = AddProfile(new MongoId(chosen));
        AddQuest(sessionId, new MongoId(alternative), QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;

        _repairStoryChapters.Repair(pmc);

        Assert.That(pmc.GetQuestStatus(new MongoId(alternative)), Is.EqualTo(QuestStatusEnum.Fail));
    }

    [Test]
    public void RepairStoryChapters_FinishedChaptersLiveLeavesAsTheyAre_StayUntouched()
    {
        // Tour ends with the escape task still running, Falling Skies' dollar collection runs during the SUV task and
        // They Are Already Here's specs task fails once the dialogue variable passes it
        var tour = new MongoId("68cbd33676fe74b1e80bfd91");
        var theyAreAlreadyHere = new MongoId("6903d779fdfc4078740a4bd0");
        var suvTask = new MongoId("678f6bd1e8d46e40ff021605");
        var escapeTask = new MongoId("69029e0aede95b6cf90c04f7");
        var sessionId = AddProfile(
            new MongoId("68c6a8551dfbf3fb78018562"),
            new MongoId("678fa1463977eb69290a3a06"),
            new MongoId("68588edc157cc3165608f2b7")
        );
        AddQuest(sessionId, tour, QuestStatusEnum.Success);
        AddQuest(sessionId, escapeTask, QuestStatusEnum.Started);
        AddQuest(sessionId, suvTask, QuestStatusEnum.Started);
        AddQuest(sessionId, theyAreAlreadyHere, QuestStatusEnum.Success);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.Variables![new MongoId("68ebbae7ddc4efdeedfdd875")] = 2;
        var before = pmc.Quests!.ToDictionary(q => q.QId, q => q.Status);

        _repairStoryChapters.Repair(pmc);

        Assert.That(pmc.Quests!.Where(q => before[q.QId] != q.Status).Select(q => q.QId), Is.Empty);
    }

    [TestCase("690b65b50bb26b80c40db801")]
    [TestCase("68e1a9e8ac971c444c06fad7")]
    public void GetClientQuests_TicketCaseRoute_StartsTheUnlockAndMechanicTasks(string route)
    {
        // Lightkeeper's hidden step after handing the case over, or talking to Kerman after keeping it
        var sessionId = AddProfile(new MongoId(route));
        AddQuest(sessionId, TicketChapter, QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.GetQuestStatus(new MongoId("68e2d8d52a2449612b06ac9b")), Is.EqualTo(QuestStatusEnum.Started), "unlock the case");
        Assert.That(pmc.GetQuestStatus(new MongoId("67bc801c7801bf5c41017b6d")), Is.EqualTo(QuestStatusEnum.Started), "ask Mechanic");
    }

    [TestCase(3.9, false)]
    [TestCase(4.0, true)]
    public void GetClientQuests_TicketFenceAssignment_NeedsTheTalkAndFenceStanding(double standing, bool started)
    {
        var fenceAssignment = new MongoId("67bdf2cecf0cc29475092199");
        var sessionId = AddProfile(new MongoId("68ea9b0b8bfb4ae0aa04ddf9"));
        AddQuest(sessionId, TicketChapter, QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.TradersInfo![Traders.FENCE] = new TraderInfo { LoyaltyLevel = 1, Standing = standing, Unlocked = true };

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.GetQuestStatus(fenceAssignment) == QuestStatusEnum.Started, Is.EqualTo(started));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void GetClientQuests_TicketIntelligenceCenterTask_OnlyWhileTheCenterIsUnbuilt(bool built, bool started)
    {
        // Given once Falling Skies ends with the armored case handed over
        var intelligenceTask = new MongoId("68e19fee596a40531902e73c");
        var sessionId = AddProfile(new MongoId("68cbcdc4c964ab83cc0c928e"), new MongoId("679cdee4ce3a208fee0ad657"));
        AddQuest(sessionId, TicketChapter, QuestStatusEnum.Started);
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        pmc.TradersInfo![Traders.KERMAN] = new TraderInfo { LoyaltyLevel = 1, Standing = 0, Unlocked = true };
        pmc.Hideout = new Hideout { Areas = [new BotHideoutArea { Type = HideoutAreas.IntelligenceCenter, Level = built ? 1 : 0 }] };

        _questHelper.GetClientQuests(sessionId);

        Assert.That(pmc.GetQuestStatus(intelligenceTask) == QuestStatusEnum.Started, Is.EqualTo(started));
    }

    private static readonly MongoId TicketChapter = new("68da33fe00868edcb6025ac4");

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
