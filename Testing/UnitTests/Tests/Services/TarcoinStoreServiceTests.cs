using NUnit.Framework;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;

namespace UnitTests.Tests.Services;

[TestFixture]
public class TarcoinStoreServiceTests
{
    private const string UsecDayOffBundle = "69c406ef12580b42af81a7ca";
    private const string UsecDayOffUpper = "69c2543d6a781e156fe07ef2";
    private const string UsecDayOffLower = "69c2545afb4c3b7b35c2da74";

    private TarcoinStoreService _service = default!;

    [SetUp]
    public void Setup()
    {
        _service = DI.GetInstance().GetService<TarcoinStoreService>();
    }

    [Test]
    public void CollectDeliverables_Bundle_DeliversItsParts()
    {
        var deliverables = _service.CollectDeliverables(_service.GetOffer(UsecDayOffBundle)!, out var bought, out var missing);

        Assert.That(
            deliverables.Select(entry => entry.CustomizationId?.ToString()),
            Is.EquivalentTo(new[] { "685d09b4029bc4c1190e819d", "685d092aed4e253164064e05" })
        );
        Assert.That(bought, Is.EquivalentTo(new[] { UsecDayOffBundle, UsecDayOffUpper, UsecDayOffLower }));
        Assert.That(missing, Is.Empty);
    }

    // The upper lists the bundles it is sold in as related offers
    [Test]
    public void CollectDeliverables_SingleOffer_IgnoresLinkedBundles()
    {
        var deliverables = _service.CollectDeliverables(_service.GetOffer(UsecDayOffUpper)!, out var bought, out _);

        Assert.That(deliverables.Select(entry => entry.CustomizationId?.ToString()), Is.EqualTo(new[] { "685d09b4029bc4c1190e819d" }));
        Assert.That(bought, Is.EquivalentTo(new[] { UsecDayOffUpper }));
    }

    [Test]
    public void EveryOffer_DeliversSomethingKnown()
    {
        foreach (var offer in DI.GetInstance().GetService<ShopTable>().Content.Offers)
        {
            var deliverables = _service.CollectDeliverables(offer, out _, out var missing);

            Assert.That(missing, Is.Empty, offer.Id.ToString());
            Assert.That(deliverables, Is.Not.Empty, offer.Id.ToString());
        }
    }

    // The Hot page's one real money tile is a full width row, so the rows under it move up into its place
    [Test]
    public void GetPage_RealMoneyHidden_ClosesTheRowItLeaves()
    {
        var page = _service.GetPage("6a481ca9b88b2d3b4fd4bb5a")!;

        Assert.That(page.Blocks.Select(block => block.PurchaseMethod), Has.All.EqualTo("INTERNAL_CURRENCY"));
        Assert.That(page.Blocks.Max(block => block.Position.Y), Is.EqualTo(3));
        Assert.That(page.Blocks.Select(block => block.Position.Y).Distinct().OrderBy(row => row), Is.EqualTo(new[] { 0, 1, 2, 3 }));
    }

    [Test]
    public void IsDeliverable_PveUpgrade_IsNot()
    {
        var pveUpgrade = DI.GetInstance()
            .GetService<ShopTable>()
            .Content.Offers.Single(offer => offer.Items.Any(entry => entry.Type == "PVE_MODE"));

        Assert.That(_service.IsDeliverable(pveUpgrade), Is.False);
    }

    [Test]
    public void GetMenu_RealMoneyHidden_DropsTabsLeftEmpty()
    {
        var shown = _service.GetMenu().Select(item => item.Id.ToString()).ToList();

        Assert.That(shown, Does.Not.Contain("69cba58925b5e944b4d5116e"));
        Assert.That(shown, Does.Not.Contain("6a22dbe90b05431dfebe972c"));
        Assert.That(shown, Does.Contain("6a4a25c93f6f25450e4ace3f"));
    }

    [Test]
    public void GetStock_BundleWithEveryPartBought_HasNothingLeft()
    {
        var profile = new SptProfile { PurchasedShopOffers = [UsecDayOffUpper, UsecDayOffLower] };

        Assert.That(_service.GetStock(profile, _service.GetOffer(UsecDayOffBundle)!), Is.EqualTo((2, 0)));
    }

    [Test]
    public void GetStock_BundleWithOnePartBought_StillHasTheOther()
    {
        var profile = new SptProfile { PurchasedShopOffers = [UsecDayOffUpper] };

        Assert.That(_service.GetStock(profile, _service.GetOffer(UsecDayOffBundle)!), Is.EqualTo((1, 1)));
    }

    // Both veteran packs of 26 parts
    [Test]
    public void GetStock_PackOfBundles_CountsEveryPart()
    {
        var stock = _service.GetStock(new SptProfile(), _service.GetOffer("6a1dcce9e38e8a06bec54415")!);

        Assert.That(stock, Is.EqualTo((0, 52)));
    }

    [Test]
    public void GetStock_SuitAlreadyUnlocked_CountsAsOwned()
    {
        var profile = new SptProfile { CustomisationUnlocks = [new CustomisationStorage { Id = new MongoId("685d09b4029bc4c1190e819d") }] };

        Assert.That(_service.GetStock(profile, _service.GetOffer(UsecDayOffUpper)!), Is.EqualTo((1, 0)));
    }

    [Test]
    public void ApplyHideoutCustomisation_RaveBundle_SetsWallFloorAndCeiling()
    {
        var pmc = new PmcData { Hideout = new Hideout { Customization = new Dictionary<string, MongoId>() } };

        foreach (var entry in _service.CollectDeliverables(_service.GetOffer("6a96bfbd206a3a67556896fe")!, out _, out _))
        {
            TarcoinStoreService.ApplyHideoutCustomisation(pmc, entry);
        }

        Assert.That(
            pmc.Hideout.Customization.ToDictionary(pair => pair.Key, pair => pair.Value.ToString()),
            Is.EquivalentTo(
                new Dictionary<string, string>
                {
                    { "Wall", "6a86df54697fb20fa70d37bd" },
                    { "Floor", "6a86df2254304bc4eb06c37a" },
                    { "Ceiling", "6a86de41697fb20fa70d37b8" },
                }
            )
        );
    }

    [Test]
    public void ApplyHideoutCustomisation_Suit_LeavesTheHideoutAlone()
    {
        var pmc = new PmcData { Hideout = new Hideout { Customization = new Dictionary<string, MongoId>() } };

        foreach (var entry in _service.CollectDeliverables(_service.GetOffer(UsecDayOffBundle)!, out _, out _))
        {
            TarcoinStoreService.ApplyHideoutCustomisation(pmc, entry);
        }

        Assert.That(pmc.Hideout.Customization, Is.Empty);
    }

    [Test]
    public void CarryOverPurchases_Wipe_KeepsOnlyWhatTheShopGave()
    {
        var questSuit = new MongoId();
        var oldProfile = new SptProfile
        {
            CharacterData = new Characters
            {
                PmcData = new PmcData
                {
                    TarCoinBalance = 500,
                    BattlePassUniversalDocumentBalance = 3,
                    Bonuses = [new Bonus { Type = BonusType.StashRows, Value = 5 }],
                },
            },
            CustomisationUnlocks = [new CustomisationStorage { Id = questSuit, Source = CustomisationSource.QUEST }],
            PurchasedShopOffers = [UsecDayOffBundle, UsecDayOffUpper, UsecDayOffLower],
            PurchasedShopStashRows = 2,
        };
        var newProfile = new SptProfile
        {
            CharacterData = new Characters
            {
                PmcData = new PmcData { TarCoinBalance = 100, Bonuses = [] },
            },
            CustomisationUnlocks = [],
        };

        _service.CarryOverPurchases(oldProfile, newProfile);

        var pmc = newProfile.CharacterData.PmcData;
        Assert.That(pmc.TarCoinBalance, Is.EqualTo(500));
        Assert.That(pmc.BattlePassUniversalDocumentBalance, Is.EqualTo(3));
        Assert.That(
            newProfile.CustomisationUnlocks.Select(unlock => unlock.Id.ToString()),
            Is.EquivalentTo(new[] { "685d09b4029bc4c1190e819d", "685d092aed4e253164064e05" })
        );
        Assert.That(pmc.Bonuses.Single(bonus => bonus.Type == BonusType.StashRows).Value, Is.EqualTo(2));
        Assert.That(newProfile.PurchasedShopOffers, Is.EquivalentTo(oldProfile.PurchasedShopOffers));
    }

    [Test]
    public void CarryOverPurchases_FirstProfile_KeepsTheEditionBalance()
    {
        var newProfile = new SptProfile
        {
            CharacterData = new Characters
            {
                PmcData = new PmcData { TarCoinBalance = 100, Bonuses = [] },
            },
        };

        _service.CarryOverPurchases(new SptProfile { CharacterData = new Characters { PmcData = new PmcData() } }, newProfile);

        Assert.That(newProfile.CharacterData.PmcData.TarCoinBalance, Is.EqualTo(100));
        Assert.That(newProfile.CharacterData.PmcData.Bonuses, Is.Empty);
    }
}
