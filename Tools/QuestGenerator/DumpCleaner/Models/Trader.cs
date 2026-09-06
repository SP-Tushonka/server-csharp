// read contents of input folder
public class Trader
{
    public string _id { get; set; }
    public bool availableInRaid { get; set; }
    public bool customization_seller { get; set; }
    public string name { get; set; }
    public string surname { get; set; }
    public string nickname { get; set; }
    public string location { get; set; }
    public string avatar { get; set; }
    public int balance_rub { get; set; }
    public int balance_dol { get; set; }
    public int balance_eur { get; set; }
    public bool unlockedByDefault { get; set; }
    public object discount { get; set; }
    public int discount_end { get; set; }
    public bool buyer_up { get; set; }
    public string currency { get; set; }
    public int nextResupply { get; set; }
    public object repair { get; set; }
    public object insurance { get; set; }
    public bool medic { get; set; }
    public int gridHeight { get; set; }
    public List<object> loyaltyLevels { get; set; }
    public List<string> sell_category { get; set; }
    public ItemsBuy items_buy { get; set; }
    public bool isCanTransferItems { get; set; }
    public ItemsBuy items_buy_prohibited { get; set; }
    public object prohibitedTransferableItems { get; set; }
    public object transferableItems { get; set; }
    public int sell_modifier_for_prohibited_items { get; set; }
}

public class ItemsBuy
{
    public string[] category { get; set; }
    public string[] id_list { get; set; }
}
