using System.Text.RegularExpressions;

namespace LootDumpProcessor.Utils;

public static class FileDateParser
{
    // New dump format embeds a Unix timestamp (seconds), e.g. "...start_1781974189.json"
    private static readonly Regex _unixTimestampRegex = new(@"start_([0-9]+)", RegexOptions.Compiled);

    // Legacy dump format embeds a "yyyy-MM-dd_HH-mm-ss" timestamp
    private static readonly Regex _fileDateRegex = new(
        ".*([0-9]{4})[-]([0-9]{2})[-]([0-9]{2})[_]([0-9]{2})[-]([0-9]{2})[-]([0-9]{2}).*",
        RegexOptions.Compiled
    );

    public static bool TryParseFileDate(string fileName, out DateTime? date)
    {
        date = null;

        var unixMatch = _unixTimestampRegex.Match(fileName);
        if (unixMatch.Success)
        {
            date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(unixMatch.Groups[1].Value)).UtcDateTime;
            return true;
        }

        if (!_fileDateRegex.IsMatch(fileName))
            return false;
        var match = _fileDateRegex.Match(fileName);
        var year = match.Groups[1].Value;
        var month = match.Groups[2].Value;
        var day = match.Groups[3].Value;
        var hour = match.Groups[4].Value;
        var mins = match.Groups[5].Value;
        var secs = match.Groups[6].Value;
        date = new DateTime(int.Parse(year), int.Parse(month), int.Parse(day), int.Parse(hour), int.Parse(mins), int.Parse(secs));
        return true;
    }
}
