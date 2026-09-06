using System.Text.RegularExpressions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.BattlePass;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using Path = System.IO.Path;

namespace AssortGenerator;

// Builds traders/<id>/assort.json and questassort.json from getTraderAssort dumps in the dumper's
// format. Season pass offers are removed from the base assort and their real prices are kept for
// the server to append per profile once the reward is claimed.
public static class Program
{
    private static readonly JsonUtil Json = new([new SptJsonConverterRegistrator()]);
    private static readonly Regex DumpName = new(@"getTraderAssort\.([0-9a-f]{24})_(\d+)\.json$", RegexOptions.Compiled);
    private static readonly MongoId Fence = new("579dc571d53a0658a154fbec");

    private static readonly Dictionary<CurrencyType, MongoId> CurrencyTpl = new()
    {
        [CurrencyType.RUB] = Money.ROUBLES,
        [CurrencyType.USD] = Money.DOLLARS,
        [CurrencyType.EUR] = Money.EUROS,
        [CurrencyType.GP] = Money.GP,
    };

    private static Dictionary<MongoId, double> _handbookPrices;

    public static void Main()
    {
        var input = Path.Combine(Directory.GetCurrentDirectory(), "input");
        var output = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(input);

        var database = ServerDatabase();
        var quests = Json.DeserializeFromFile<Dictionary<MongoId, Quest>>(Path.Combine(database, "templates", "quests.json"));
        var pass = Json.DeserializeFromFile<BattlePassActiveResponse>(Path.Combine(database, "season", "battlePass.json"));
        var handbook = Json.DeserializeFromFile<HandbookBase>(Path.Combine(database, "templates", "handbook.json"));
        _handbookPrices = handbook.Items.ToDictionary(item => item.Id, item => item.Price ?? 0);

        var dumpsByTrader = Directory
            .GetFiles(input, "*.json")
            .Select(path => (Path: path, Match: DumpName.Match(path)))
            .Where(entry => entry.Match.Success)
            .GroupBy(entry => new MongoId(entry.Match.Groups[1].Value));

        var passRewards = (pass.BattlePasses ?? [])
            .SelectMany(entry => entry.Pages ?? [])
            .SelectMany(page => page.Rewards ?? [])
            .SelectMany(cell => (cell.Rewards ?? []).Select(reward => (Cell: cell, Reward: reward)))
            .Where(entry => entry.Reward.Type == RewardType.AssortmentUnlock)
            .ToList();
        var passOffers = new Dictionary<MongoId, BattlePassAssortOffer>();

        foreach (var group in dumpsByTrader)
        {
            var traderId = group.Key;
            if (traderId == Fence)
            {
                continue;
            }

            var baseJson = Path.Combine(database, "traders", traderId, "base.json");
            var trader = File.Exists(baseJson) ? Json.DeserializeFromFile<TraderBase>(baseJson) : null;
            var currency = CurrencyTpl[trader?.Currency ?? CurrencyType.RUB];

            var ordered = group.OrderBy(entry => long.Parse(entry.Match.Groups[2].Value)).Select(entry => entry.Path).ToList();
            var assort = Merge(ordered);
            Console.WriteLine(
                $"{trader?.Nickname ?? traderId}: {ordered.Count} dumps, {assort.Items.Count(item => item.ParentId == "hideout")} offers"
            );

            StripSeasonPassOffers(assort, traderId, passRewards, passOffers);
            var questAssort = BuildQuestAssort(assort, traderId, quests, currency);

            var traderOutput = Path.Combine(output, "traders", traderId);
            Directory.CreateDirectory(traderOutput);
            File.WriteAllText(Path.Combine(traderOutput, "assort.json"), Json.Serialize(assort, true));
            File.WriteAllText(Path.Combine(traderOutput, "questassort.json"), Json.Serialize(questAssort, true));
        }

        var seasonOutput = Path.Combine(output, "season");
        Directory.CreateDirectory(seasonOutput);
        File.WriteAllText(Path.Combine(seasonOutput, "battlePassAssort.json"), Json.Serialize(passOffers, true));
        Console.WriteLine($"season pass offers with real prices: {passOffers.Count} of {passRewards.Count}");
    }

    // The newest dump wins for every id, older dumps only add offers the newer profile did not see
    private static TraderAssort Merge(List<string> paths)
    {
        var items = new Dictionary<MongoId, Item>();
        var barter = new Dictionary<MongoId, List<List<BarterScheme>>>();
        var loyalty = new Dictionary<MongoId, int>();
        foreach (var path in paths)
        {
            var dump = Json.DeserializeFromFile<TraderAssort>(path);
            foreach (var item in dump.Items)
            {
                items[item.Id] = item;
            }

            foreach (var (id, scheme) in dump.BarterScheme)
            {
                barter[id] = scheme;
            }

            foreach (var (id, level) in dump.LoyalLevelItems)
            {
                loyalty[id] = level;
            }
        }

        var roots = items.Values.Where(item => item.ParentId == "hideout").ToList();
        var kept = new List<Item>();
        foreach (var root in roots)
        {
            if (!barter.ContainsKey(root.Id) || !loyalty.ContainsKey(root.Id))
            {
                Console.WriteLine($"  offer {root.Id} ({root.Template}) has no price or loyalty level, dropped");
                continue;
            }

            if (root.Upd is not null)
            {
                root.Upd.BuyRestrictionCurrent = 0;
            }

            kept.Add(root);
            kept.AddRange(Children(items, root.Id));
        }

        var keptRoots = kept.Where(item => item.ParentId == "hideout").Select(item => item.Id).ToHashSet();
        return new TraderAssort
        {
            Items = kept,
            BarterScheme = barter.Where(entry => keptRoots.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value),
            LoyalLevelItems = loyalty.Where(entry => keptRoots.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value),
        };
    }

    private static List<Item> Children(Dictionary<MongoId, Item> items, MongoId parentId)
    {
        var result = new List<Item>();
        var parent = parentId.ToString();
        foreach (var child in items.Values.Where(item => item.ParentId == parent))
        {
            result.Add(child);
            result.AddRange(Children(items, child.Id));
        }

        return result;
    }

    // A profile that claimed a pass reward shows it as a normal offer. Those are removed from the
    // base assort and their price is remembered for the per profile injection on the server.
    private static void StripSeasonPassOffers(
        TraderAssort assort,
        MongoId traderId,
        List<(BattlePassPageReward Cell, Reward Reward)> passRewards,
        Dictionary<MongoId, BattlePassAssortOffer> passOffers
    )
    {
        foreach (var (cell, reward) in passRewards.Where(entry => entry.Reward.TraderId?.ToString() == traderId))
        {
            var match = FindOffer(assort, reward);
            if (match is null)
            {
                continue;
            }

            passOffers[cell.Id] = new BattlePassAssortOffer
            {
                RewardId = reward.Id,
                BarterScheme = assort.BarterScheme[match.Id],
                LoyaltyLevel = assort.LoyalLevelItems[match.Id],
            };
            Remove(assort, match.Id);
            Console.WriteLine($"  season pass offer {reward.Target} ({match.Template}) removed, price kept");
        }
    }

    private static Dictionary<string, Dictionary<MongoId, MongoId>> BuildQuestAssort(
        TraderAssort assort,
        MongoId traderId,
        Dictionary<MongoId, Quest> quests,
        MongoId currency
    )
    {
        var questAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
        {
            ["started"] = [],
            ["success"] = [],
            ["fail"] = [],
        };

        foreach (var (questId, quest) in quests)
        {
            foreach (var (state, rewards) in quest.Rewards ?? [])
            {
                if (!questAssort.TryGetValue(state.ToLowerInvariant(), out var bucket))
                {
                    continue;
                }

                foreach (
                    var reward in rewards.Where(entry =>
                        entry.Type == RewardType.AssortmentUnlock && entry.TraderId?.ToString() == traderId
                    )
                )
                {
                    var match = FindOffer(assort, reward) ?? Inject(assort, reward, currency, questId);
                    if (match is null)
                    {
                        continue;
                    }

                    // The server keys unlocks by offer, so an offer two quests share stays with the first
                    var taken = questAssort.Values.FirstOrDefault(entry => entry.ContainsKey(match.Id));
                    if (taken is not null)
                    {
                        Console.WriteLine($"  offer {match.Template} already unlocked by quest {taken[match.Id]}, {questId} skipped");
                        continue;
                    }

                    bucket[match.Id] = questId;
                }
            }
        }

        return questAssort;
    }

    // Reward item ids are regenerated per dump, so offers are matched by template and tier
    private static Item FindOffer(TraderAssort assort, Reward reward)
    {
        var root = reward.Items?.FirstOrDefault(item => item.Id == reward.Target);
        if (root is null)
        {
            return null;
        }

        var candidates = assort.Items.Where(item => item.ParentId == "hideout" && item.Template == root.Template).ToList();
        return candidates.FirstOrDefault(item => assort.LoyalLevelItems.GetValueOrDefault(item.Id) == reward.LoyaltyLevel)
            ?? candidates.FirstOrDefault();
    }

    // No captured profile had unlocked this offer, so it goes in with a handbook derived price
    private static Item Inject(TraderAssort assort, Reward reward, MongoId currency, MongoId questId)
    {
        var root = reward.Items?.FirstOrDefault(item => item.Id == reward.Target);
        if (root is null)
        {
            return null;
        }

        root.ParentId = "hideout";
        root.SlotId = "hideout";
        root.Upd ??= new Upd();
        root.Upd.UnlimitedCount = true;
        root.Upd.StackObjectsCount = 999999;
        assort.Items.AddRange(reward.Items);
        assort.LoyalLevelItems[root.Id] = reward.LoyaltyLevel ?? 1;
        assort.BarterScheme[root.Id] =
        [
            [new BarterScheme { Template = currency, Count = DerivedPrice(reward.Items, currency) }],
        ];
        Console.WriteLine($"  quest offer {root.Template} for quest {questId} not in any dump, added with a handbook price");

        return root;
    }

    private static double DerivedPrice(IEnumerable<Item> items, MongoId currency)
    {
        var roubles = items.Sum(item => _handbookPrices.GetValueOrDefault(item.Template) * (item.Upd?.StackObjectsCount ?? 1));
        var rate = currency == Money.ROUBLES ? 1 : Math.Max(1, _handbookPrices.GetValueOrDefault(currency));
        return Math.Round(roubles / rate, 2);
    }

    private static void Remove(TraderAssort assort, MongoId rootId)
    {
        var ids = new HashSet<string> { rootId.ToString() };
        var added = true;
        while (added)
        {
            added = false;
            foreach (var item in assort.Items.Where(item => !ids.Contains(item.Id.ToString()) && ids.Contains(item.ParentId ?? "")))
            {
                ids.Add(item.Id.ToString());
                added = true;
            }
        }

        assort.Items.RemoveAll(item => ids.Contains(item.Id.ToString()));
        assort.BarterScheme.Remove(rootId);
        assort.LoyalLevelItems.Remove(rootId);
    }

    private static string ServerDatabase()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Libraries", "SPTushonka.Server.Assets", "SPT_Data", "database");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new Exception($"Could not find the server database above {AppContext.BaseDirectory}");
    }
}
