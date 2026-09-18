using NUnit.Framework;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

namespace UnitTests.Tests.Helpers;

[TestFixture]
public class HealthHelperTests
{
    private const string MildMusclePain = "MildMusclePain";
    private const double PainSeconds = 300;

    private HealthHelper _helper = default!;
    private TimeUtil _timeUtil = default!;

    [OneTimeSetUp]
    public void Initialize()
    {
        _helper = DI.GetInstance().GetService<HealthHelper>();
        _timeUtil = DI.GetInstance().GetService<TimeUtil>();
    }

    [Test]
    public void ApplyHealthChangesToProfile_ClientReportsMildMusclePain_AddsIt()
    {
        var pmc = Pmc(secondsSinceUpdate: 0);

        _helper.ApplyHealthChangesToProfile(pmc, ClientHealth(withPain: true), false);

        Assert.That(Chest(pmc).Effects![MildMusclePain]!.Time, Is.EqualTo(PainSeconds));
    }

    [Test]
    public void ApplyHealthChangesToProfile_ClientNoLongerReportsMildMusclePain_RemovesIt()
    {
        var pmc = Pmc(secondsSinceUpdate: 0, withPain: true);

        _helper.ApplyHealthChangesToProfile(pmc, ClientHealth(withPain: false), false);

        Assert.That(Chest(pmc).Effects!.ContainsKey(MildMusclePain), Is.False);
    }

    [Test]
    public void UpdateProfileHealthValues_MildMusclePainOutlastsElapsedTime_CountsItDown()
    {
        var pmc = Pmc(secondsSinceUpdate: 100, withPain: true);

        _helper.UpdateProfileHealthValues(pmc);

        Assert.That(Chest(pmc).Effects![MildMusclePain]!.Time, Is.EqualTo(PainSeconds - 100).Within(2));
    }

    [Test]
    public void UpdateProfileHealthValues_MildMusclePainRunsOut_RemovesIt()
    {
        var pmc = Pmc(secondsSinceUpdate: PainSeconds + 60, withPain: true);

        _helper.UpdateProfileHealthValues(pmc);

        Assert.That(Chest(pmc).Effects!.ContainsKey(MildMusclePain), Is.False);
    }

    [Test]
    public void UpdateProfileHealthValues_RunTwice_CountsElapsedTimeOnce()
    {
        var pmc = Pmc(secondsSinceUpdate: 100, withPain: true);

        _helper.UpdateProfileHealthValues(pmc);
        _helper.UpdateProfileHealthValues(pmc);

        Assert.That(Chest(pmc).Effects![MildMusclePain]!.Time, Is.EqualTo(PainSeconds - 100).Within(2));
    }

    [Test]
    public void UpdateProfileHealthValues_GivenEffectTimeUpdater_UsesItInstead()
    {
        var pmc = Pmc(secondsSinceUpdate: 100, withPain: true);
        double? elapsed = null;

        _helper.UpdateProfileHealthValues(pmc, (_, _, diffSeconds) => elapsed = diffSeconds);

        Assert.That(elapsed, Is.EqualTo(100).Within(2));
        Assert.That(Chest(pmc).Effects![MildMusclePain]!.Time, Is.EqualTo(PainSeconds));
    }

    [Test]
    public void UpdateProfileHealthValues_SkippedEffectStoredAsNull_LeavesIt()
    {
        var pmc = Pmc(secondsSinceUpdate: 100);
        Chest(pmc).Effects!["Exhaustion"] = null;

        Assert.DoesNotThrow(() => _helper.UpdateProfileHealthValues(pmc));
        Assert.That(Chest(pmc).Effects!.ContainsKey("Exhaustion"), Is.True);
    }

    private static BodyPartHealth Chest(PmcData pmc)
    {
        return pmc.Health!.BodyParts!["Chest"];
    }

    private PmcData Pmc(double secondsSinceUpdate, bool withPain = false)
    {
        var health = ClientHealth(withPain);
        health.UpdateTime = _timeUtil.GetTimeStamp() - secondsSinceUpdate;

        return new PmcData
        {
            Health = health,
            Bonuses = [],
            Inventory = new BotBaseInventory { Items = [] },
        };
    }

    private static BotBaseHealth ClientHealth(bool withPain)
    {
        var effects = new Dictionary<string, BodyPartEffectProperties?>();
        if (withPain)
        {
            effects[MildMusclePain] = new BodyPartEffectProperties { Time = PainSeconds };
        }

        return new BotBaseHealth
        {
            Hydration = Vital(),
            Energy = Vital(),
            Temperature = Vital(),
            BodyParts = new Dictionary<string, BodyPartHealth>
            {
                ["Chest"] = new()
                {
                    Health = new CurrentMinMax { Current = 85, Maximum = 85 },
                    Effects = effects,
                },
            },
        };
    }

    private static CurrentMinMax Vital()
    {
        return new CurrentMinMax { Current = 100, Maximum = 100 };
    }
}
