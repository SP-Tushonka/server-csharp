using System.Globalization;
using SPTarkov.Server.Core.Models.Eft.Game;

namespace SPTarkov.Server.Web.Pages.Shop.Components;

public static class ShopFormat
{
    // Prices group with a non breaking space ("4 500"). The separator has to be forced, or a server
    // running under a European culture renders "4.500".
    public static string Price(int amount)
    {
        return amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", "\u00A0");
    }

    // A WIDGET offer goes through Xsolla for real money and carries no tarcoin amount, so it shows
    // no price and cannot be bought here.
    public static bool IsTarcoinPriced(ShopBlock block)
    {
        return block.PurchaseMethod == "INTERNAL_CURRENCY";
    }

    // Live pads a tile's item count to two digits, so a four item bundle reads "04".
    public static string Quantity(int count)
    {
        return count.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
    }
}
