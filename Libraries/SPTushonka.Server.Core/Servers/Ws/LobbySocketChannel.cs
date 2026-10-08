using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Servers.Ws;

/// <summary>
///     Open lobby sockets and the binary frames sent on them. Kept apart from <see cref="SessionRequestWebSocketHandler"/>
///     so notification senders do not depend on the http router.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class LobbySocketChannel(JsonUtil jsonUtil)
{
    // A frame is a fixed header naming the size of each part, then the three parts back to back.
    //   0  message type        byte
    //   1  trackingId length   uint32 big endian
    //   5  payloadType length  uint32 big endian
    //   9  payload length      uint64 big endian
    //  17  trackingId, then payloadType, then payload
    private const int MessageTypeOffset = 0;
    private const int TrackingIdLengthOffset = 1;
    private const int PayloadTypeLengthOffset = 5;
    private const int PayloadLengthOffset = 9;
    private const int FrameHeaderSize = 17;

    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _sendGates = [];

    /// <summary>
    ///     Open lobby socket of each session, the one notifications are pushed on
    /// </summary>
    private readonly ConcurrentDictionary<MongoId, WebSocket> _sessionSockets = [];

    /// <summary>
    ///     Make a socket the one notifications for the session are pushed on
    /// </summary>
    /// <param name="sessionId">Session the socket belongs to</param>
    /// <param name="ws">Newly opened socket</param>
    public void Register(MongoId sessionId, WebSocket ws)
    {
        _sessionSockets[sessionId] = ws;
    }

    /// <summary>
    ///     Forget a closed socket
    /// </summary>
    /// <param name="sessionId">Session the socket belonged to</param>
    /// <param name="ws">Closed socket</param>
    public void Unregister(MongoId sessionId, WebSocket ws)
    {
        // A reconnect may already have replaced this socket, only drop the entry when it is still ours
        _sessionSockets.TryRemove(new KeyValuePair<MongoId, WebSocket>(sessionId, ws));

        if (_sendGates.TryRemove(ws, out var gate))
        {
            gate.Dispose();
        }
    }

    /// <summary>
    ///     Push a notification to the session as a Notification frame
    /// </summary>
    /// <param name="sessionId">Session to notify</param>
    /// <param name="notification">Notification to send</param>
    /// <returns>True when the frame was handed to an open socket</returns>
    public async Task<bool> SendNotificationAsync(MongoId sessionId, WsNotificationEvent notification)
    {
        if (!_sessionSockets.TryGetValue(sessionId, out var ws) || ws.State != WebSocketState.Open)
        {
            return false;
        }

        // The client reads the payload as an array of notifications and the type from the frame header
        var json = jsonUtil.Serialize(notification, notification.GetType()) ?? string.Empty;
        await SendFrameAsync(
            ws,
            WsWireMessageType.Notification,
            notification.EventIdentifier.ToString(),
            notification.EventType?.ToString() ?? string.Empty,
            $"[{json}]",
            CancellationToken.None
        );

        return true;
    }

    /// <summary>
    ///     Send one frame, holding a per socket gate so two sends cannot interleave
    /// </summary>
    /// <param name="ws">Socket to send on</param>
    /// <param name="messageType">Kind of frame</param>
    /// <param name="trackingId">Id the client matches the frame against its pending request by</param>
    /// <param name="payloadType">Route or notification type the frame belongs to, empty on a confirmation</param>
    /// <param name="payload">Frame body, empty on a confirmation</param>
    /// <param name="cancellationToken">Token cancelled when the connection drops</param>
    public async Task SendFrameAsync(
        WebSocket ws,
        WsWireMessageType messageType,
        string trackingId,
        string payloadType,
        string payload,
        CancellationToken cancellationToken
    )
    {
        var frame = EncodeFrame(messageType, trackingId, payloadType, payload);
        var gate = _sendGates.GetOrAdd(ws, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(frame, WebSocketMessageType.Binary, true, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    ///     Build a frame the way the client reads one, a type byte then the three part lengths big endian
    /// </summary>
    /// <param name="messageType">Kind of frame</param>
    /// <param name="trackingId">Id the frame answers</param>
    /// <param name="payloadType">Route the frame belongs to</param>
    /// <param name="payload">Frame body</param>
    /// <returns>Encoded frame</returns>
    private static byte[] EncodeFrame(WsWireMessageType messageType, string trackingId, string payloadType, string payload)
    {
        var id = Encoding.UTF8.GetBytes(trackingId);
        var route = Encoding.UTF8.GetBytes(payloadType);
        var body = Encoding.UTF8.GetBytes(payload);

        Span<byte> header = stackalloc byte[FrameHeaderSize];
        header[MessageTypeOffset] = (byte)messageType;
        BinaryPrimitives.WriteUInt32BigEndian(header[TrackingIdLengthOffset..], (uint)id.Length);
        BinaryPrimitives.WriteUInt32BigEndian(header[PayloadTypeLengthOffset..], (uint)route.Length);
        BinaryPrimitives.WriteUInt64BigEndian(header[PayloadLengthOffset..], (ulong)body.Length);

        return [.. header, .. id, .. route, .. body];
    }

    /// <summary>
    ///     Split a frame into its three parts, rejecting anything whose lengths do not account for every byte
    /// </summary>
    /// <param name="data">Whole frame as received</param>
    /// <param name="frame">The decoded frame</param>
    /// <returns>True when the frame was laid out as expected</returns>
    public static bool TryDecodeFrame(byte[] data, out WsWireFrame frame)
    {
        frame = default;

        if (data.Length < FrameHeaderSize)
        {
            return false;
        }

        ReadOnlySpan<byte> header = data.AsSpan(0, FrameHeaderSize);
        ReadOnlySpan<byte> rest = data.AsSpan(FrameHeaderSize);
        if (
            !TryTakePart(ref rest, BinaryPrimitives.ReadUInt32BigEndian(header[TrackingIdLengthOffset..]), out var trackingId)
            || !TryTakePart(ref rest, BinaryPrimitives.ReadUInt32BigEndian(header[PayloadTypeLengthOffset..]), out var payloadType)
            || !TryTakePart(ref rest, BinaryPrimitives.ReadUInt64BigEndian(header[PayloadLengthOffset..]), out var payload)
            || !rest.IsEmpty
        )
        {
            return false;
        }

        frame = new WsWireFrame((WsWireMessageType)header[MessageTypeOffset], trackingId, payloadType, payload);

        return true;
    }

    /// <summary>
    ///     Read the next part of a frame as text and move past it
    /// </summary>
    /// <param name="rest">Bytes not yet read, shortened by the part on success</param>
    /// <param name="length">Part length the header gives</param>
    /// <param name="part">The part as text</param>
    /// <returns>False when the header names more bytes than the frame holds</returns>
    private static bool TryTakePart(ref ReadOnlySpan<byte> rest, ulong length, out string part)
    {
        if (length > (ulong)rest.Length)
        {
            part = string.Empty;
            return false;
        }

        var bytes = rest[..(int)length];
        rest = rest[bytes.Length..];
        part = Encoding.UTF8.GetString(bytes);

        return true;
    }
}

/// <summary>
///     One frame split into the parts its header describes
/// </summary>
/// <param name="MessageType">Kind of frame</param>
/// <param name="TrackingId">Id the client matches the frame against its pending request by</param>
/// <param name="PayloadType">Route or notification type the frame belongs to</param>
/// <param name="Payload">Frame body</param>
public readonly record struct WsWireFrame(WsWireMessageType MessageType, string TrackingId, string PayloadType, string Payload);

/// <summary>
///     Kind of frame, as the client writes it into the first byte
/// </summary>
public enum WsWireMessageType : byte
{
    Notification = 0,
    Confirm = 1,
    Request = 2,
    Response = 3,
}
