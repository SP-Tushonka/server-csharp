using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SPTarkov.Server.Core.Helpers.Traders;

[Injectable]
public class BattlePassAssortHelper(
    ISptLogger<BattlePassAssortHelper> logger,
    SeasonTable seasonTable,
    TradersTable traderTable,
    HandbookHelper handbookHelper,
    ICloner cloner
)
{
    private static readonly Dictionary<CurrencyType, MongoId> CurrencyTpl = new()
    {
        [CurrencyType.RUB] = Money.ROUBLES,
        [CurrencyType.USD] = Money.DOLLARS,
        [CurrencyType.EUR] = Money.EUROS,
        [CurrencyType.GP] = Money.GP,
    };

    public void AppendClaimedOffers(PmcData? pmcProfile, MongoId traderId, TraderAssort assort)
    {
        var progress = pmcProfile?.BattlePassProgress;
        if (progress is null || progress.Count == 0)
        {
            return;
        }

        foreach (var pass in seasonTable.BattlePass.BattlePasses ?? [])
        {
            var claimed = progress.FirstOrDefault(entry => entry.BattlePassId == pass.Id)?.ObtainedRewardIds;
            if (claimed is null || claimed.Count == 0)
            {
                continue;
            }

            var cells = (pass.Pages ?? []).SelectMany(page => page.Rewards ?? []).Where(cell => claimed.Contains(cell.Id));
            foreach (var cell in cells)
            {
                foreach (var reward in cell.Rewards ?? [])
                {
                    if (reward.Type != RewardType.AssortmentUnlock || reward.TraderId?.ToString() != traderId)
                    {
                        continue;
                    }

                    Append(assort, traderId, cell.Id, reward);
                }
            }
        }
    }

    private void Append(TraderAssort assort, MongoId traderId, MongoId cellId, Reward reward)
    {
        var items = cloner.Clone(reward.Items) ?? [];
        var root = items.FirstOrDefault(item => item.Id == reward.Target);
        if (root is null || assort.Items.Any(item => item.Id == root.Id))
        {
            return;
        }

        root.ParentId = "hideout";
        root.SlotId = "hideout";
        root.Upd ??= new Upd();
        root.Upd.UnlimitedCount = true;
        root.Upd.StackObjectsCount = 999999;

        var known = seasonTable.BattlePassAssort?.GetValueOrDefault(cellId);
        assort.Items.AddRange(items);
        assort.LoyalLevelItems[root.Id] = known?.LoyaltyLevel ?? reward.LoyaltyLevel ?? 1;
        assort.BarterScheme[root.Id] = known?.BarterScheme ?? DerivedPrice(traderId, items);

        if (known is null && logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"Season pass offer {root.Template} from {traderId} has no captured price, using the handbook");
        }
    }

    private List<List<BarterScheme>> DerivedPrice(MongoId traderId, List<Item> items)
    {
        var currency = CurrencyTpl[traderTable.GetTrader(traderId)?.Base?.Currency ?? CurrencyType.RUB];
        var roubles = handbookHelper.GetTemplatePriceForItems(items);
        var count = currency == Money.ROUBLES ? roubles : handbookHelper.FromRoubles(roubles, currency);

        return
        [
            [new BarterScheme { Template = currency, Count = Math.Round(count, 2) }],
        ];
    }
}
