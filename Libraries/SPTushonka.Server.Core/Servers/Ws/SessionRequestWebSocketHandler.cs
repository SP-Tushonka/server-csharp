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
///     Backend routes the client sends over the Lobby websocket instead of http. Each request is answered with a
///     Confirm frame and then a Response frame carrying the same tracking id.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class SessionRequestWebSocketHandler(
    ISptLogger<SessionRequestWebSocketHandler> logger,
    HttpRouter httpRouter,
    LobbySocketChannel lobbySocketChannel,
    JsonUtil jsonUtil
) : IWebSocketConnectionHandler
{
    public const string HookUrl = "/ws/session/";

    private static readonly JsonSerializerOptions RequestOptions = new() { PropertyNameCaseInsensitive = true };

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

        lobbySocketChannel.Register(new MongoId(GetSessionId(context)), ws);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Answer one framed request, confirming receipt before the route runs so the client stops its retries
    /// </summary>
    /// <param name="rawData">Whole frame as received</param>
    /// <param name="messageType">Kind of websocket message the frame arrived as</param>
    /// <param name="ws">Socket the frame arrived on</param>
    /// <param name="context">Connection the socket belongs to</param>
    public async Task OnMessageAsync(byte[] rawData, WebSocketMessageType messageType, WebSocket ws, HttpContext context)
    {
        if (messageType != WebSocketMessageType.Binary)
        {
            logger.Warning($"[WS] Request channel ignored a {messageType} message of {rawData.Length} bytes: {Preview(rawData)}");
            return;
        }

        if (!LobbySocketChannel.TryDecodeFrame(rawData, out var frame))
        {
            logger.Warning($"[WS] Request channel could not decode a {rawData.Length} byte frame: {Preview(rawData)}");
            return;
        }

        // The client confirms every Response and Notification it receives, nothing waits on those
        if (frame.MessageType == WsWireMessageType.Confirm)
        {
            return;
        }

        if (frame.MessageType != WsWireMessageType.Request)
        {
            logger.Warning($"[WS] Request channel ignored a {frame.MessageType} frame for {frame.TrackingId}");
            return;
        }

        var request = ReadRequest(frame.Payload);
        if (request is null)
        {
            return;
        }

        await lobbySocketChannel.SendFrameAsync(
            ws,
            WsWireMessageType.Confirm,
            frame.TrackingId,
            string.Empty,
            string.Empty,
            context.RequestAborted
        );

        var sessionId = new MongoId(GetSessionId(context));
        var response = await BuildResponseAsync(request, sessionId, context.RequestAborted);
        var json = jsonUtil.Serialize(response) ?? string.Empty;
        await lobbySocketChannel.SendFrameAsync(
            ws,
            WsWireMessageType.Response,
            frame.TrackingId,
            frame.PayloadType,
            json,
            context.RequestAborted
        );
    }

    /// <summary>
    ///     Read the request body a Request frame carries
    /// </summary>
    /// <param name="json">Request body</param>
    /// <returns>The request, or null when it cannot be used</returns>
    private WsRequestMessage? ReadRequest(string json)
    {
        WsRequestMessage? request;
        try
        {
            request = JsonSerializer.Deserialize<WsRequestMessage>(json, RequestOptions);
        }
        catch (Exception ex)
        {
            logger.Error($"[WS] Request channel could not read a request: {ex.Message}");
            return null;
        }

        if (request?.Method is null || request.Id is null)
        {
            logger.Warning($"[WS] Request channel received a request without method or id: {Truncate(json)}");
            return null;
        }

        return request;
    }

    public Task OnCloseAsync(WebSocket ws, HttpContext context, string sessionIdContext)
    {
        lobbySocketChannel.Unregister(new MongoId(GetSessionId(context)), ws);

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

    private static string GetSessionId(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path[(path.IndexOf(HookUrl, StringComparison.Ordinal) + HookUrl.Length)..].Trim('/');
    }

    private static string Truncate(string text)
    {
        return text.Length <= 200 ? text : text[..200];
    }

    /// <summary>
    ///     Render the start of a message as hex and as text, so a frame the channel cannot read can still be identified
    /// </summary>
    /// <param name="rawData">Message bytes</param>
    /// <returns>Hex of the first bytes followed by their printable characters</returns>
    private static string Preview(byte[] rawData)
    {
        var head = rawData.AsSpan(0, Math.Min(rawData.Length, 512));
        var text = string.Concat(Encoding.ASCII.GetString(head).Select(c => char.IsControl(c) ? '.' : c));

        return $"{Convert.ToHexString(head)} [{text}]";
    }
}
