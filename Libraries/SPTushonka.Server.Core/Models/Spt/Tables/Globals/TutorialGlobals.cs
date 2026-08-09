namespace SPTarkov.Server.Core.Models.Spt.Tables.Globals;

public record class TutorialGlobals
{
    public required string BrokenWeaponId { get; set; }

    public required string TutorAutoStartQuestId { get; set; }

    public required TutorialWeather TutorialWeather { get; set; }
}

public record TutorialWeather
{
    public required double AtmospherePressure { get; set; }

    public required double Cloudness { get; set; }

    public required double LyingWater { get; set; }

    public required double Rain { get; set; }

    public required double RainRandomness { get; set; }

    public required double ScaterringFogDensity { get; set; }

    public required double Temperature { get; set; }

    public required int TimeHour { get; set; }

    public required int TimeMinute { get; set; }

    public required double Turbulence { get; set; }

    public required double Wind { get; set; }

    public required double WindDirection { get; set; }
}
