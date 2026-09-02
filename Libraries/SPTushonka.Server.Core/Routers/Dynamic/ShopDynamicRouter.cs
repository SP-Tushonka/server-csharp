using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Routers.Dynamic;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class ShopDynamicRouter(JsonUtil jsonUtil, GameCallbacks gameCallbacks)
    : DynamicRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/v2/shop/api/v1/account/balance",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetShopBalance(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/v2/shop/api/v1/menu",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetShopMenu(url, info, sessionID)
            ),
            new RouteAction<EmptyRequestData>(
                "/v2/shop/api/v1/catalog/",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.GetShopCatalogItem(url, info, sessionID)
            ),
            new RouteAction<ShopPurchaseRequest>(
                "/v2/shop/api/v1/purchase/single",
                async (url, info, sessionID, output, cancellationToken) => await gameCallbacks.PurchaseShopOffer(url, info, sessionID)
            ),
        ]
    )
{ }
