using NUnit.Framework;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Profile;

namespace UnitTests.Tests.Services;

[TestFixture]
public class InitialSupportServiceTests
{
    private InitialSupportService _service = default!;
    private SaveServer _saveServer = default!;
    private InitialSupportSettings _settings = default!;

    [SetUp]
    public void Setup()
    {
        _service = DI.GetInstance().GetService<InitialSupportService>();
        _saveServer = DI.GetInstance().GetService<SaveServer>();
        _settings = DI.GetInstance().GetService<GiftsConfig>().InitialSupport!;
    }

    [Test]
    public async Task SendDueCrate_NewCharacter_SendsOneCrateADay()
    {
        var sessionId = await CreateUsec();
        _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Info!.RegistrationDate -= 2 * 86400;

        Assert.That(_service.SendDueCrate(sessionId), Is.True, "first crate");
        Assert.That(_service.SendDueCrate(sessionId), Is.False, "same day");

        var record = _saveServer.GetProfile(sessionId).SptData!.ReceivedGifts!.Single(gift => gift.GiftId == InitialSupportService.GiftId);
        record.TimestampLastAccepted -= 25 * 3600;

        Assert.That(_service.SendDueCrate(sessionId), Is.True, "next day");

        var crates = SupportMessages(sessionId);
        Assert.That(crates.Select(message => message.TemplateId), Is.EqualTo(new[] { _settings.FirstMessageId, _settings.MessageId }));
        Assert.That(crates.All(message => _settings.Containers.Contains(message.Items!.Data!.Single().Template)), Is.True);
    }

    [Test]
    public async Task SendDueCrate_CharacterOlderThanTheWindow_SendsNothing()
    {
        var sessionId = await CreateUsec();
        _saveServer.GetProfile(sessionId).CharacterData!.PmcData!.Info!.RegistrationDate -= (_settings.Days + 1) * 86400;

        Assert.That(_service.SendDueCrate(sessionId), Is.False);
        Assert.That(SupportMessages(sessionId), Is.Empty);
    }

    private List<Message> SupportMessages(MongoId sessionId)
    {
        return (_saveServer.GetProfile(sessionId).DialogueRecords ?? [])
            .Values.SelectMany(dialogue => dialogue.Messages ?? [])
            .Where(message => message.MessageType == MessageType.InitialSupport)
            .OrderBy(message => message.DateTime)
            .ToList();
    }

    private static async Task<MongoId> CreateUsec()
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
                    Edition = "Standard",
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
