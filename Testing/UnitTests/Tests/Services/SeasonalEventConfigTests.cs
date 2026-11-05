using NUnit.Framework;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Server.SeasonalEvents;

namespace UnitTests.Tests.Services;

[TestFixture]
public class SeasonalEventConfigTests
{
    [TestCase(SeasonalEventType.HalloweenSummon)]
    [TestCase(SeasonalEventType.Khorovod)]
    [TestCase(SeasonalEventType.Runddans)]
    public void Events_LegacyEvent_IsOffByDefaultAndHasAnApplier(SeasonalEventType type)
    {
        var config = DI.GetInstance().GetService<SeasonalEventConfig>();
        var appliers = DI.GetInstance().GetService<IEnumerable<ISeasonalEventApplier>>();

        var entry = config.Events.SingleOrDefault(seasonalEvent => seasonalEvent.Type == type);

        Assert.That(entry, Is.Not.Null, "an entry lets players switch it on");
        Assert.That(entry!.Enabled, Is.False);
        Assert.That(entry.AlwaysActive, Is.False);
        Assert.That(appliers.Count(applier => applier.Type == type), Is.EqualTo(1));
    }
}
