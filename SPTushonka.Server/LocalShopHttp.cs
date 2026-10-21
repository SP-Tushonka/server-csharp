using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTarkov.Server;

/// <summary>Explicit local store listener; the normal game API remains HTTPS.</summary>
public static class LocalShopHttp
{
    public const string EnableVariable = LocalShopHttpOptions.EnableVariable;
    public const int Port = LocalShopHttpOptions.Port;
    public static bool Enabled => LocalShopHttpOptions.Enabled;

    public static void Configure(KestrelServerOptions options)
    {
        if (!Enabled) return;
        var config = options.ApplicationServices.GetRequiredService<HttpConfig>();
        var address = IPAddress.Parse(config.Ip);
        if (!IPAddress.IsLoopback(address))
            throw new InvalidOperationException("Local store mode requires a loopback backend.");
        options.Listen(address, config.Port, listener => listener.UseHttps());
        options.Listen(IPAddress.Loopback, Port);
    }

    public static bool AllowedPath(PathString path) =>
        path.StartsWithSegments("/shop") || path.StartsWithSegments("/_blazor") ||
        path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content") ||
        path.StartsWithSegments("/SPTarkov.Server.Web") || path.StartsWithSegments("/files/shop") ||
        path.StartsWithSegments("/v2/shop") || path.StartsWithSegments("/notifierServer/getwebsocket");

    public static async Task Guard(HttpContext context, RequestDelegate next)
    {
        if (context.Connection.LocalPort == Port)
        {
            if (!Enabled || context.Connection.RemoteIpAddress is not { } remote ||
                !IPAddress.IsLoopback(remote) || !AllowedPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }
        await next(context);
    }
}
