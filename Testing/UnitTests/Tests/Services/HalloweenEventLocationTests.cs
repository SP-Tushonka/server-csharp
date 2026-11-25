using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Services.Server.SeasonalEvents;

namespace UnitTests.Tests.Services;

[TestFixture]
public class HalloweenEventLocationTests
{
    private LocationTable _locationTable = default!;
    private SeasonalEventService _seasonalEventService = default!;
    private SeasonalEventEffects _effects = default!;

    [OneTimeSetUp]
    public void Initialize()
    {
        _locationTable = DI.GetInstance().GetService<LocationTable>();
        _seasonalEventService = DI.GetInstance().GetService<SeasonalEventService>();
        _effects = DI.GetInstance().GetService<SeasonalEventEffects>();
    }

    [Test]
    public void AfterStartup_EventMaps_AreOnlyEnabledWhileHalloweenIsActive()
    {
        var halloweenActive = _seasonalEventService.HalloweenEventEnabled();

        Assert.That(_locationTable.FactoryEvent!.Base.Enabled, Is.EqualTo(halloweenActive));
        Assert.That(_locationTable.LaboratoryEvent!.Base.Enabled, Is.EqualTo(halloweenActive));
    }

    [Test]
    public void SetHalloweenEventLocationsEnabled_TogglesBothEventMaps()
    {
        var originalFactory = _locationTable.FactoryEvent!.Base.Enabled;
        var originalLabs = _locationTable.LaboratoryEvent!.Base.Enabled;

        try
        {
            _effects.SetHalloweenEventLocationsEnabled(true);
            Assert.That(_locationTable.FactoryEvent.Base.Enabled, Is.True);
            Assert.That(_locationTable.LaboratoryEvent.Base.Enabled, Is.True);

            _effects.SetHalloweenEventLocationsEnabled(false);
            Assert.That(_locationTable.FactoryEvent.Base.Enabled, Is.False);
            Assert.That(_locationTable.LaboratoryEvent.Base.Enabled, Is.False);
        }
        finally
        {
            _locationTable.FactoryEvent.Base.Enabled = originalFactory;
            _locationTable.LaboratoryEvent.Base.Enabled = originalLabs;
        }
    }
}
