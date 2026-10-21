using System.Net;
using Microsoft.AspNetCore.Http;
using SPTarkov.Server;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Ws;

var passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
var old = Environment.GetEnvironmentVariable(LocalShopHttp.EnableVariable);
try
{
    Environment.SetEnvironmentVariable(LocalShopHttp.EnableVariable, null);
    Check(!LocalShopHttp.Enabled, "off by default");
    Environment.SetEnvironmentVariable(LocalShopHttp.EnableVariable, "0");
    Check(!LocalShopHttp.Enabled, "explicit off");
    Environment.SetEnvironmentVariable(LocalShopHttp.EnableVariable, "true");
    try { _ = LocalShopHttp.Enabled; throw new Exception("invalid setting accepted"); }
    catch (InvalidOperationException) { passed++; }
    Environment.SetEnvironmentVariable(LocalShopHttp.EnableVariable, "1");
    Check(new NotifierHelper(null!).GetDefaultNotification() is WsPing, "real notifier keep-alive constructs successfully");
    Check(new NotifierHelper(null!).GetWebSocketServer(new("000000000000000000000001")) == "ws://127.0.0.1:6970/notifierServer/getwebsocket/000000000000000000000001", "opt-in notifier advertises plain loopback websocket");
    foreach (var path in new[] { "/shop/token", "/shop/token/catalog-item/id", "/_blazor/negotiate", "/_framework/blazor.web.js", "/_content/MudBlazor/a.css", "/SPTarkov.Server.Web/js/expansions-shop.js", "/files/shop/image.png", "/v2/shop/api/v1/menu", "/notifierServer/getwebsocket/session" })
        Check(LocalShopHttp.AllowedPath(path), "store path " + path);
    foreach (var path in new[] { "/shop-evil", "/profiles/id/download", "/launcher/profile/info", "/client/game/config", "/files/elsewhere", "/v2/shop-evil", "/notifierServer/get/session", "/notifierServer/getwebsocket-evil/session", "/" })
        Check(!LocalShopHttp.AllowedPath(path), "blocked path " + path);
    async Task Test(int port, string address, string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.LocalPort = port;
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        context.Request.Path = path;
        var called = false;
        await LocalShopHttp.Guard(context, _ => { called = true; return Task.CompletedTask; });
        Check(called == expected && (called || context.Response.StatusCode == 404), $"guard {port}/{address}/{path}");
    }
    await Test(6970, "127.0.0.1", "/shop/token", true);
    await Test(6970, "127.0.0.1", "/notifierServer/getwebsocket/session", true);
    await Test(6970, "::1", "/_blazor", true);
    await Test(6970, "192.0.2.1", "/shop/token", false);
    await Test(6970, "127.0.0.1", "/profiles/id/download", false);
    await Test(6969, "127.0.0.1", "/client/game/config", true);
    Environment.SetEnvironmentVariable(LocalShopHttp.EnableVariable, "0");
    await Test(6970, "127.0.0.1", "/shop/token", false);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { passed, failed = 0, networkUsed = false, serverStarted = false }));
}
finally { Environment.SetEnvironmentVariable(LocalShopHttp.EnableVariable, old); }
