using NUnit.Framework;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;

namespace UnitTests.Tests.Services;

[TestFixture]
public class FenceServiceTests
{
    [TestCase(-20.0, -7)]
    [TestCase(3.4, 3)]
    [TestCase(7.5, 6)]
    [TestCase(99.0, 6)]
    [TestCase(100.0, 100)]
    [TestCase(150.0, 100)]
    public void GetFenceInfo_StandingBetweenLevels_UsesTheLevelBelow(double standing, double expectedLevel)
    {
        var fenceService = DI.GetInstance().GetService<FenceService>();
        var fenceSettings = DI.GetInstance().GetService<GlobalTable>().Configuration.FenceSettings;
        var pmc = new PmcData
        {
            TradersInfo = new Dictionary<MongoId, TraderInfo> { [fenceSettings.FenceIdentifier] = new() { Standing = standing } },
        };

        Assert.That(fenceService.GetFenceInfo(pmc), Is.SameAs(fenceSettings.Levels[expectedLevel]));
    }
}
