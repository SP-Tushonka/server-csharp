using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.HttpResponse;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Servers;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Servers.Ws;

/// <summary>
///     Backend routes the 1.1 client sends over the Lobby websocket instead of http. Each request is
///     answered with "CONFIRM {id}" and then "RESPONSE {id} {method} {json}".
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class SessionRequestWebSocketHandler(
    ISptLogger<SessionRequestWebSocketHandler> logger,
    HttpRouter httpRouter,
    JsonUtil jsonUtil
) : IWebSocketConnectionHandler
{
    public const string HookUrl = "/ws/session/";

    private static readonly char[] Separators = [' ', '\n', '\r'];

    private static readonly JsonSerializerOptions RequestOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _sendGates = new();

    public string GetHookUrl()
    {
        return HookUrl;
    }

    public string GetSocketId()
    {
        return "SPT session request channel";
    }

    public Task OnConnectionAsync(WebSocket ws, HttpContext context, string sessionIdContext)
    {
        if (logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"[WS] Request channel opened for session {GetSessionId(context)} with context {sessionIdContext}");
        }

        return Task.CompletedTask;
    }

    public async Task OnMessageAsync(byte[] rawData, WebSocketMessageType messageType, WebSocket ws, HttpContext context)
    {
        if (messageType != WebSocketMessageType.Text)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(rawData);
        var parts = text.Split(Separators, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[0] != "REQUEST")
        {
            logger.Warning($"[WS] Request channel received an unknown message: {Truncate(text)}");
            return;
        }

        WsRequestMessage? request;
        try
        {
            request = JsonSerializer.Deserialize<WsRequestMessage>(parts[2], RequestOptions);
        }
        catch (Exception ex)
        {
            logger.Error($"[WS] Request channel could not read a request: {ex.Message}");
            return;
        }

        if (request?.Method is null || request.Id is null)
        {
            logger.Warning($"[WS] Request channel received a request without method or id: {Truncate(text)}");
            return;
        }

        await SendAsync(ws, $"CONFIRM {request.Id}", context.RequestAborted);

        var sessionId = new MongoId(GetSessionId(context));
        var response = await BuildResponseAsync(request, sessionId, context.RequestAborted);
        await SendAsync(ws, $"RESPONSE {request.Id} {request.Method} {jsonUtil.Serialize(response)}", context.RequestAborted);
    }

    public Task OnCloseAsync(WebSocket ws, HttpContext context, string sessionIdContext)
    {
        if (_sendGates.TryRemove(ws, out var gate))
        {
            gate.Dispose();
        }

        return Task.CompletedTask;
    }

    private async Task<WsResponseMessage> BuildResponseAsync(
        WsRequestMessage request,
        MongoId sessionId,
        CancellationToken cancellationToken
    )
    {
        var response = new WsResponseMessage { Id = request.Id, Method = request.Method };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = request.Method;
        var body = request.Params.HasValue ? request.Params.Value.GetRawText() : null;

        object? routed;
        try
        {
            routed = await httpRouter.GetResponseObjectAsync(httpContext.Request, sessionId, body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error($"[WS] {request.Method} failed: {ex}");
            response.Error = new WsResponseError { Code = "500", Message = ex.Message };
            return response;
        }

        var json = routed switch
        {
            string text => text,
            StreamedJsonBody streamed => jsonUtil.Serialize(streamed.Payload),
            _ => null,
        };

        var envelope = string.IsNullOrEmpty(json) ? null : jsonUtil.Deserialize<GetBodyResponseData<JsonElement?>>(json);
        if (envelope is null)
        {
            logger.Warning($"[WS] No handler answered {request.Method}");
            response.Error = new WsResponseError { Code = "404", Message = $"No handler for {request.Method}" };
            return response;
        }

        if (envelope.Err is not null && envelope.Err != BackendErrorCodes.None)
        {
            response.Error = new WsResponseError { Code = ((int)envelope.Err).ToString(), Message = envelope.ErrMsg ?? string.Empty };
            return response;
        }

        response.Result = envelope.Data;

        return response;
    }

    private async Task SendAsync(WebSocket ws, string text, CancellationToken cancellationToken)
    {
        var gate = _sendGates.GetOrAdd(ws, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static string GetSessionId(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path[(path.IndexOf(HookUrl, StringComparison.Ordinal) + HookUrl.Length)..].Trim('/');
    }

    private static string Truncate(string text)
    {
        return text.Length <= 200 ? text : text[..200];
    }
}
