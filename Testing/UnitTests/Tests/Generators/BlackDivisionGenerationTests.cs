using NUnit.Framework;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Servers;

namespace UnitTests.Tests.Generators;

[TestFixture]
public class BlackDivisionGenerationTests
{
    private static readonly MongoId Afak = new("60098ad7c2240c0fe85c570a");
    private static readonly MongoId Calok = new("5e8488fa988a8701445df1e4");
    private static readonly MongoId FerrumDogtag = new("6a461aed7391ab085a093760");

    private BotGenerator _generator = default!;
    private MongoId _sessionId;

    [SetUp]
    public void Setup()
    {
        _generator = DI.GetInstance().GetService<BotGenerator>();
        _sessionId = new MongoId();
        DI.GetInstance().GetService<SaveServer>().CreateProfile(new SPTarkov.Server.Core.Models.Eft.Profile.Info { ProfileId = _sessionId });
    }

    [Test]
    public void GenerateBot_WedgeAtNormal_IsAlwaysWedge()
    {
        for (var i = 0; i < 20; i++)
        {
            var bot = Generate("bossWedge", "normal");

            Assert.That(bot.Info!.Nickname, Is.EqualTo("Wedge"));
            Assert.That(bot.Inventory!.Items!.Any(item => item.SlotId == "SecondPrimaryWeapon"), Is.True, "MP7");
        }
    }

    [Test]
    public void GenerateBot_WedgeAtEasy_IsAGuardWithoutHisKit()
    {
        for (var i = 0; i < 20; i++)
        {
            var bot = Generate("bossWedge", "easy");

            Assert.That(bot.Info!.Settings!.Role, Is.EqualTo("bossWedge"));
            Assert.That(bot.Info.Nickname, Is.Not.EqualTo("Wedge"));
            Assert.That(bot.Inventory!.Items!.Any(item => item.SlotId == "SecondPrimaryWeapon"), Is.False);
        }
    }

    [Test]
    public void GenerateBot_BlackDivisionOperator_CarriesOneOfEachGuaranteedItem()
    {
        for (var i = 0; i < 20; i++)
        {
            var items = Generate("pmcBotBlackDiv", "normal").Inventory!.Items!;

            Assert.That(items.Count(item => item.Template == Afak), Is.EqualTo(1), "AFAK");
            Assert.That(items.Count(item => item.Template == Calok), Is.EqualTo(1), "CALOK-B");
            Assert.That(items.Count(item => item.Template == FerrumDogtag), Is.LessThanOrEqualTo(1), "dogtag");
        }
    }

    private BotBase Generate(string role, string difficulty)
    {
        return _generator.PrepareAndGenerateBot(
            _sessionId,
            new BotGenerationDetails
            {
                Role = role,
                RoleLowercase = role.ToLowerInvariant(),
                Side = "Savage",
                BotDifficulty = difficulty,
                BotLevel = 40,
                GameVersion = "standard",
                BotCountToGenerate = 1,
            }
        );
    }
}
