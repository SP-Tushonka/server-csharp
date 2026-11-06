using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;

namespace UnitTests.Tests.Services;

[TestFixture]
public class RagfairOfferServiceTests
{
    private const string GoodsReturnedTemplate = "5bdabfe486f7743e1665df6e 0";

    private RagfairServer _ragfairServer = default!;
    private RagfairOfferService _offerService = default!;
    private RagfairOfferHolder _offerHolder = default!;
    private SaveServer _saveServer = default!;
    private TimeUtil _timeUtil = default!;

    [SetUp]
    public void Setup()
    {
        _ragfairServer = DI.GetInstance().GetService<RagfairServer>();
        _offerService = DI.GetInstance().GetService<RagfairOfferService>();
        _offerHolder = DI.GetInstance().GetService<RagfairOfferHolder>();
        _saveServer = DI.GetInstance().GetService<SaveServer>();
        _timeUtil = DI.GetInstance().GetService<TimeUtil>();
    }

    [Test]
    public async Task Update_ExpiredPlayerOfferBelowThreshold_ReturnsItemsByMail()
    {
        var sessionId = await CreateUsec();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        var offer = CreateOffer(pmc.Id!.Value, _timeUtil.GetTimeStamp() - 10);
        pmc.RagfairInfo!.Offers!.Add(offer);
        _offerService.AddOffer(offer);

        _ragfairServer.Update();

        Assert.That(pmc.RagfairInfo.Offers, Does.Not.Contain(offer));
        Assert.That(_offerHolder.GetOfferById(offer.Id), Is.Null);

        var returned = ReturnedMessages(sessionId);
        Assert.That(returned, Has.Count.EqualTo(1));
        Assert.That(returned[0].Items!.Data!.Single().Template, Is.EqualTo(offer.Items![0].Template));
    }

    [Test]
    public async Task Update_ActivePlayerOffer_StaysListed()
    {
        var sessionId = await CreateUsec();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        var offer = CreateOffer(pmc.Id!.Value, _timeUtil.GetTimeStamp() + 3600);
        pmc.RagfairInfo!.Offers!.Add(offer);
        _offerService.AddOffer(offer);

        _ragfairServer.Update();

        Assert.That(pmc.RagfairInfo.Offers, Does.Contain(offer));
        Assert.That(_offerHolder.GetOfferById(offer.Id), Is.Not.Null);
        Assert.That(ReturnedMessages(sessionId), Is.Empty);

        _offerHolder.RemoveOffer(offer.Id);
    }

    [Test]
    public async Task Update_ExpiredOfferWithPartialSale_ReturnsOnlyUnsoldItems()
    {
        var sessionId = await CreateUsec();
        var pmc = _saveServer.GetProfile(sessionId).CharacterData!.PmcData!;
        var offer = CreateOffer(pmc.Id!.Value, _timeUtil.GetTimeStamp() + 3600, 10);
        pmc.RagfairInfo!.Offers!.Add(offer);
        _offerService.AddOffer(offer);

        DI.GetInstance().GetService<RagfairOfferHelper>().CompleteOffer(sessionId, offer, 3);
        offer.EndTime = _timeUtil.GetTimeStamp() - 10;
        _ragfairServer.Update();

        var returnedItems = ReturnedMessages(sessionId).SelectMany(message => message.Items!.Data!);
        Assert.That(returnedItems.Sum(item => item.Upd?.StackObjectsCount ?? 1), Is.EqualTo(7));
    }

    [Test]
    public void Update_ExpiredOfferOfMissingProfile_IsDropped()
    {
        var offer = CreateOffer(new MongoId(), _timeUtil.GetTimeStamp() - 10);
        _offerService.AddOffer(offer);

        Assert.DoesNotThrow(() => _ragfairServer.Update());

        Assert.That(_offerHolder.GetOfferById(offer.Id), Is.Null);
    }

    private List<Message> ReturnedMessages(MongoId sessionId)
    {
        return (_saveServer.GetProfile(sessionId).DialogueRecords ?? [])
            .Values.SelectMany(dialogue => dialogue.Messages ?? [])
            .Where(message => message.TemplateId == GoodsReturnedTemplate)
            .ToList();
    }

    private static RagfairOffer CreateOffer(MongoId ownerId, long endTime, int count = 1)
    {
        var rootItem = new Item
        {
            Id = new MongoId(),
            Template = ItemTpl.BARTER_LEDX_SKIN_TRANSILLUMINATOR,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd { StackObjectsCount = count },
        };

        return new RagfairOffer
        {
            Id = new MongoId(),
            CreatedBy = OfferCreator.Player,
            User = new RagfairOfferUser { Id = ownerId, MemberType = MemberCategory.Default },
            Root = rootItem.Id,
            Items = [rootItem],
            Requirements =
            [
                new OfferRequirement
                {
                    TemplateId = Money.ROUBLES,
                    Count = 1000,
                    OnlyFunctional = false,
                },
            ],
            SummaryCost = 1000,
            StartTime = endTime - 3600,
            EndTime = endTime,
            Quantity = count,
        };
    }

    private static async Task<MongoId> CreateUsec()
    {
        var sessionId = new MongoId();
        DI.GetInstance()
            .GetService<SaveServer>()
            .CreateProfile(
                new SPTarkov.Server.Core.Models.Eft.Profile.Info
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
