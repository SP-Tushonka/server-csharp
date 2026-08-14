using Microsoft.AspNetCore.Http;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Servers;

namespace SPTarkov.Server.Core.Routers;

[Injectable]
public class HttpRouter(IEnumerable<StaticRouter> staticRouters, IEnumerable<DynamicRouter> dynamicRoutes)
{
    public bool CanHandle(HttpContext context)
    {
        var url = context.Request.Path.Value;

        return !string.IsNullOrEmpty(url)
            && (staticRouters.Any(sr => sr.CanHandle(url, false)) || dynamicRoutes.Any(dr => dr.CanHandle(url, true)));
    }

    public async ValueTask<object?> GetResponseObjectAsync(
        HttpRequest req,
        MongoId sessionID,
        string? body,
        CancellationToken cancellationToken = default
    )
    {
        var wrapper = new ResponseWrapper("");
        var handled = await HandleRouteAsync(req, sessionID, wrapper, staticRouters, false, body, cancellationToken);
        if (!handled)
        {
            await HandleRouteAsync(req, sessionID, wrapper, dynamicRoutes, true, body, cancellationToken);
        }

        return (object?)wrapper.StreamedBody ?? wrapper.Output;
    }

    protected async ValueTask<bool> HandleRouteAsync(
        HttpRequest request,
        MongoId sessionID,
        ResponseWrapper wrapper,
        IEnumerable<Router> routers,
        bool dynamic,
        string? body,
        CancellationToken cancellationToken = default
    )
    {
        var url = request.Path.Value;

        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        // remove retry from url
        if (url.Contains("?retry="))
        {
            url = url.Split("?retry=")[0];
        }

        var matched = false;
        foreach (var route in routers)
        {
            if (!route.CanHandle(url, dynamic))
            {
                continue;
            }

            var output = wrapper.Output ?? string.Empty;

            var result = route switch
            {
                DynamicRouter dynamicRouter => await dynamicRouter.HandleDynamicAsync(url, body, sessionID, output, cancellationToken),
                StaticRouter staticRouter => await staticRouter.HandleStaticAsync(url, body, sessionID, output, cancellationToken),
                _ => null,
            };

            if (result is StreamedJsonBody streamed)
            {
                wrapper.StreamedBody = streamed;
            }
            else
            {
                wrapper.Output = result as string;
                wrapper.StreamedBody = null;
            }

            matched = true;
        }

        return matched;
    }

    protected sealed class ResponseWrapper(string? output)
    {
        public string? Output { get; set; } = output;

        public StreamedJsonBody? StreamedBody { get; set; }
    }
}
