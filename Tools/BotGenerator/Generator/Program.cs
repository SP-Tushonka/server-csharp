using System.Diagnostics;
using Common.Models;
using Common.Models.Input;
using Generator.Helpers;

namespace Generator;

internal static class Program
{
    internal static async Task Main(string[] args)
    {
        var stopwatch = Stopwatch.StartNew();
        LoggingHelpers.LogToConsole("Started processing bots");

        // Create list of bots we want to process
        string[] botTypes =
        {
            "assault",
            "assaultgroup",
            "assaulttutorial",
            "blackdivision",
            "vsrf",
            "vsrfsniper",
            "civilian",
            "sentry",
            "marksman",
            "pmcbot",
            "exusec",
            "bossbully",
            "bossgluhar",
            "bosskilla",
            "bosskojaniy",
            "bosssanitar",
            "bosstagilla",
            "bossknight",
            "bosszryachiy",
            "bossboar",
            "bossboarsniper",
            "bosskolontay",
            "bosspartisan",
            "followerbully",
            "followergluharassault",
            "followergluharscout",
            "followergluharsecurity",
            "followergluharsnipe",
            "followerkojaniy",
            "followersanitar",
            "followerstormtrooper",
            "followerbirdeye",
            "followerbigpipe",
            "followerzryachiy",
            "followerboar",
            "followerboarclose1",
            "followerboarclose2",
            "followerkolontayassault",
            "followerkolontaysecurity",
            "ravangezryachiyevent",
            "peacefullzryachiyevent",
            "cursedassault",
            "sectantpriest",
            "sectantwarrior",
            "sectantoni",
            "sectantpredvestnik",
            "sectantprizrak",
            "gifter",
            "arenafighterevent",
            "crazyassaultevent",
            "shooterbtr",
            "spiritspring",
            "spiritwinter",
            "skier",
            "peacemaker",
            "infectedassault",
            "infectedpmc",
            "infectedcivil",
            "infectedlaborant",
            "infectedtagilla",
            "pmcusec",
            "pmcbear",
            "bosstagillaagro",
            "bosskillaagro",
            "tagillahelperagro",
            "bosswedge",
            "bossbullyblackdiv",
            "followerbullyblackdiv",
            "pmcbotblackdiv",
            "bosswedgelab",
            "followerwedgelab",
        };

        // Read raw bot dumps and turn into c# objects
        var workingPath = Directory.GetCurrentDirectory();
        var dumpPath = $"{workingPath}//dumps";
        List<GeneratedBot> bots = await BotParser.Parse(dumpPath, botTypes.ToHashSet());

        if (bots.Count == 0)
        {
            LoggingHelpers.LogToConsole("No bots found, unable to continue");
            LoggingHelpers.LogToConsole("Check your dumps are in 'Generator\\bin\\Debug\\net6.0\\dumps' and start with 'resp.' NOT 'req.'");
            return;
        }

        var jsonWriter = new JsonWriter(workingPath, "output");
        jsonWriter.WriteJson(bots);

        stopwatch.Stop();
        LoggingHelpers.LogToConsole(
            $"Finished processing bots. Took {LoggingHelpers.LogTimeTaken(stopwatch.Elapsed.TotalSeconds)} seconds"
        );
    }
}
