using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Generators.Weather;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Weather;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Spt.Weather;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class WeatherController(
    TimeUtil timeUtil,
    WeatherGenerator weatherGenerator,
    SeasonalEventService seasonalEventService,
    RaidWeatherService raidWeatherService,
    WeatherHelper weatherHelper,
    WeatherConfig weatherConfig,
    ICloner cloner,
    ProfileActivityService profileActivityService,
    LocationTable locationTable
)
{
    /// <summary>
    ///     Handle client/weather
    /// </summary>
    /// <returns>WeatherData</returns>
    public WeatherData Generate()
    {
        var currentSeason = seasonalEventService.GetActiveWeatherSeason();

        // Prep object to send to client
        var result = new WeatherData
        {
            Acceleration = 0,
            Time = string.Empty,
            Date = string.Empty,
            Weather = null,
            Season = currentSeason,
        };

        // Assign now in a bsg-style formatted string to result object property
        result.Date = timeUtil.GetDateTimeNow().FormatToBsgDate();

        // Get server uptime seconds multiplied by a multiplier and add to current time as seconds
        result.Time = weatherHelper.GetInRaidTime().GetBsgFormattedWeatherTime();
        result.Acceleration = weatherConfig.Acceleration;

        var presetWeights = cloner.Clone(weatherGenerator.GetWeatherPresetWeightsBySeason(currentSeason));
        result.Weather = weatherGenerator.GenerateWeather(result.Season.Value, ref presetWeights);

        return result;
    }

    /// <summary>
    ///     Handle client/localGame/weather
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns>GetLocalWeatherResponseData</returns>
    public GetLocalWeatherResponseData GenerateLocal(MongoId sessionId)
    {
        var result = new GetLocalWeatherResponseData { Season = seasonalEventService.GetActiveWeatherSeason(), Weather = [] };

        var upcomingWeather = raidWeatherService.GetUpcomingWeather();
        var fixedWeather = GetRaidFixedWeather(sessionId);
        if (fixedWeather is null)
        {
            result.Weather.AddRange(upcomingWeather);
            return result;
        }

        // The client applies the map's fixed weather and then replaces it with this response, so it has to be sent here
        result.Weather.AddRange(upcomingWeather.Select(weather => ApplyFixedWeather(weather, fixedWeather)));

        return result;
    }

    /// <summary>
    ///     Get the fixed weather of the map the player is about to raid
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns>Fixed weather of the map, null when the map has none</returns>
    protected FixedWeatherLocationConfig? GetRaidFixedWeather(MongoId sessionId)
    {
        var locationId = profileActivityService.GetProfileActivityRaidData(sessionId).RaidConfiguration?.Location;
        if (locationId is null)
        {
            return null;
        }

        return locationTable.GetLocation(locationId)?.Base?.FixedWeatherLocationConfig;
    }

    /// <summary>
    ///     Copy a forecast entry with the map's fixed weather values, keeping its time and gustiness
    /// </summary>
    /// <param name="weather">Forecast entry to copy</param>
    /// <param name="fixedWeather">Fixed weather of the map</param>
    /// <returns>Forecast entry with the fixed weather</returns>
    protected static Weather ApplyFixedWeather(Weather weather, FixedWeatherLocationConfig fixedWeather)
    {
        return weather with
        {
            Cloud = fixedWeather.Cloudness,
            WindSpeed = fixedWeather.Wind,
            WindDirection = (WindDirection?)fixedWeather.WindDirection,
            Rain = fixedWeather.Rain,
            RainIntensity = fixedWeather.RainRandomness,
            Fog = fixedWeather.ScaterringFogDensity,
            Temperature = fixedWeather.Temperature,
            Pressure = fixedWeather.AtmospherePressure,
        };
    }
}
