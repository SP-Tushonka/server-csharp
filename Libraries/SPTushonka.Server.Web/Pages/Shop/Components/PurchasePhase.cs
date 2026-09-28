namespace SPTarkov.Server.Web.Pages.Shop.Components;

/// <summary>
///     Steps of the purchase dialog, in order
/// </summary>
public enum PurchasePhase
{
    Confirm,
    Transfer,
    Reveal,
    Accepted,
}
