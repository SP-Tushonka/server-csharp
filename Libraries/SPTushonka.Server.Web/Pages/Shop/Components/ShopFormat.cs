using System.Globalization;
using SPTarkov.Server.Core.Models.Eft.Game;

namespace SPTarkov.Server.Web.Pages.Shop.Components;

public static class ShopFormat
{
    /// <summary>
    ///     Format a price grouped with a non breaking space, as "4 500". The separator is forced because a
    ///     server running under a European culture would render "4.500"
    /// </summary>
    /// <param name="amount">Price in TarCoins</param>
    /// <returns>Formatted price</returns>
    public static string Price(int amount)
    {
        return amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", "\u00A0");
    }

    /// <summary>
    ///     Check whether a tile is priced in TarCoins. A WIDGET offer goes through Xsolla for real money and cannot be bought here
    /// </summary>
    /// <param name="block">Tile to check</param>
    /// <returns>True for a TarCoin offer</returns>
    public static bool IsTarcoinPriced(ShopBlock block)
    {
        return block.PurchaseMethod == "INTERNAL_CURRENCY";
    }

    /// <summary>
    ///     Get the texture for a tile tag
    /// </summary>
    /// <param name="tag">Tag such as HOT or NEW</param>
    /// <returns>Texture url, or null for a tag that is not drawn</returns>
    public static string? TagTexture(string tag)
    {
        return tag switch
        {
            "NEW" => "/files/shop/ExpansionsHub_Tag_Full_1.png",
            "SALE" => "/files/shop/ExpansionsHub_Tag_Full_2.png",
            "FREE" => "/files/shop/ExpansionsHub_Tag_Full_3.png",
            "HOT" => "/files/shop/ExpansionsHub_Tag_Full_4.png",
            "LIMITED" => "/files/shop/ExpansionsHub_Tag_Full_5.png",
            _ => null,
        };
    }

    /// <summary>
    ///     Get the marker texture a menu label draws beside its tab
    /// </summary>
    /// <param name="label">Label such as NEW or SALE</param>
    /// <returns>Texture url, or null for a label that is not drawn</returns>
    public static string? MarkerTexture(string label)
    {
        return label switch
        {
            "NEW" => "/files/shop/ExpansionsHub_Navigation_States_Icon_1.png",
            "FREE" => "/files/shop/ExpansionsHub_Navigation_States_Icon_2.png",
            "SALE" => "/files/shop/ExpansionsHub_Navigation_States_Icon_3.png",
            _ => null,
        };
    }

    /// <summary>
    ///     Check whether a tile is tagged as free
    /// </summary>
    /// <param name="tags">Tile tags</param>
    /// <returns>True when tagged FREE</returns>
    public static bool IsFree(IEnumerable<string> tags)
    {
        return tags.Contains("FREE");
    }

    /// <summary>
    ///     Get the discount percentage from the two amounts, rounding half away from zero
    /// </summary>
    /// <param name="price">Offer price</param>
    /// <returns>Percentage off, or null when the offer is not discounted</returns>
    public static int? DiscountPercent(ShopPrice? price)
    {
        if (price is null || price.Amount <= 0 || price.Amount <= price.AmountWithDiscount)
        {
            return null;
        }

        return (int)Math.Round((price.Amount - price.AmountWithDiscount) * 100.0 / price.Amount, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     Pad a tile's item count to two digits, so a four item bundle reads "04"
    /// </summary>
    /// <param name="count">Items in the offer</param>
    /// <returns>Padded count</returns>
    public static string Quantity(int count)
    {
        return count.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
    }
}
