using System.Globalization;
using System.Text.RegularExpressions;
using LootDumpProcessor.Logger;

namespace LootDumpProcessor.Process.Reader.Filters;

public class JsonDumpFileFilter : IFileFilter
{
    // New dump format embeds a Unix timestamp (seconds), e.g. "...start_1781974189.json"
    private static readonly Regex _fileNameDateRegex = new(@"start_([0-9]+)", RegexOptions.Compiled);
    private static readonly DateTime _parsedThresholdDate;

    static JsonDumpFileFilter()
    {
        // Calculate parsed date from config threshold
        if (string.IsNullOrEmpty(LootDumpProcessorContext.GetConfig().ReaderConfig.ThresholdDate))
        {
            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Warning))
                LoggerFactory
                    .GetInstance()
                    .Log($"ThresholdDate is null or empty in configs, defaulting to current day minus 30 days", LogLevel.Warning);
            _parsedThresholdDate = (DateTime.Now - TimeSpan.FromDays(30));
        }
        else
        {
            _parsedThresholdDate = DateTime.ParseExact(
                LootDumpProcessorContext.GetConfig().ReaderConfig.ThresholdDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );
        }
    }

    public string GetExtension() => "json";

    public bool Accept(string filename)
    {
        var match = _fileNameDateRegex.Match(filename);
        if (!match.Success)
            return false;

        var date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(match.Groups[1].Value)).UtcDateTime;
        return date > _parsedThresholdDate;
    }
}
