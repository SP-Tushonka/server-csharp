using NUnit.Framework;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Profile;

namespace UnitTests.Tests.Controllers;

[TestFixture]
public class EndingControllerTests
{
    private static readonly MongoId DebtorEnding = new("68a6028ef4c23ebbbc49da4b");
    private static readonly MongoId DebtorFinisher = new("67c9877aff0329206209cb67");
    private static readonly MongoId Aa12Gen2 = new("67124dcfa3541f2a1f0e788b");

    [Test]
    public async Task ObtainEnding_FinishableQuest_IsHandedInWithRewards()
    {
        var sessionId = await CreateDeveloperUsec();
        var saveServer = DI.GetInstance().GetService<SaveServer>();
        var fullProfile = saveServer.GetProfile(sessionId);
        var pmc = fullProfile.CharacterData!.PmcData!;
        pmc.Quests!.First(quest => quest.QId == DebtorFinisher).Status = QuestStatusEnum.AvailableForFinish;

        await DI.GetInstance().GetService<EndingController>().ObtainEnding(sessionId, new EndingRequest { EndingId = DebtorEnding });

        var mailedTemplates = (fullProfile.DialogueRecords ?? [])
            .Values.SelectMany(dialogue => dialogue.Messages ?? [])
            .SelectMany(message => message.Items?.Data ?? [])
            .Select(item => item.Template)
            .ToList();
        Assert.That(pmc.Quests!.First(quest => quest.QId == DebtorFinisher).Status, Is.EqualTo(QuestStatusEnum.Success));
        Assert.That(mailedTemplates.Count(template => template == Aa12Gen2), Is.EqualTo(3));
    }

    [Test]
    public async Task ObtainEnding_QuestNotFinishable_IsLeftAlone()
    {
        var sessionId = await CreateDeveloperUsec();
        var pmc = DI.GetInstance().GetService<SaveServer>().GetProfile(sessionId).CharacterData!.PmcData!;

        await DI.GetInstance().GetService<EndingController>().ObtainEnding(sessionId, new EndingRequest { EndingId = DebtorEnding });

        Assert.That(pmc.Quests!.First(quest => quest.QId == DebtorFinisher).Status, Is.EqualTo(QuestStatusEnum.Started));
    }

    private static async Task<MongoId> CreateDeveloperUsec()
    {
        var sessionId = new MongoId();
        DI.GetInstance()
            .GetService<SaveServer>()
            .CreateProfile(
                new Info
                {
                    ProfileId = sessionId,
                    ScavengerId = new MongoId(),
                    Aid = 1,
                    Username = sessionId.ToString(),
                    Edition = "SPT Developer",
                }
            );
        await DI.GetInstance()
            .GetService<CreateProfileService>()
            .CreateProfile(
                sessionId,
                new ProfileCreateRequestData
                {
                    Side = "Usec",
                    Nickname = sessionId.ToString()[..15],
                    HeadId = new MongoId("62aca6a1310e67685a2fc2e7"),
                    VoiceId = new MongoId("5fc1223595572123ae7384a3"),
                }
            );

        return sessionId;
    }
}
