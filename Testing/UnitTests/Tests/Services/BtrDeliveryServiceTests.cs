using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Utils;

namespace UnitTests.Tests.Services;

[TestFixture]
public class BtrDeliveryServiceTests
{
    private BtrDeliveryService _service = default!;
    private BtrDeliveryConfig _config = default!;
    private TimeUtil _timeUtil = default!;

    [SetUp]
    public void Setup()
    {
        _service = DI.GetInstance().GetService<BtrDeliveryService>();
        _config = DI.GetInstance().GetService<BtrDeliveryConfig>();
        _timeUtil = DI.GetInstance().GetService<TimeUtil>();
    }

    [Test]
    public void GetBTRDeliveryReturnTimestamp_DefaultConfig_ArrivesWithinTheConfiguredRange()
    {
        var now = _timeUtil.GetTimeStamp();

        var arrival = _service.GetBTRDeliveryReturnTimestamp();

        Assert.That(arrival - now, Is.InRange(_config.ReturnTimeSeconds.Min - 1, _config.ReturnTimeSeconds.Max + 1));
        Assert.That(_config.ReturnTimeSeconds.Min, Is.GreaterThan(0));
    }
}
