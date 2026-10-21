using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Servers.Ws;

var passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
var request = "REQUEST /client/mail/msg/send {\"method\":\"/client/mail/msg/send\",\"params\":{\"dialogId\":\"000000000000000000000001\",\"text\":\"GREEDISGOOD\"},\"id\":\"71\"}";
Check(LobbyTextFrame.TryDecode(Utf8(request), out var frame), "actual text request shape accepted");
Check(frame.MessageType == WsWireMessageType.Request && frame.TrackingId == "71" && frame.PayloadType == "/client/mail/msg/send", "correlated id and method");
Check(JsonDocument.Parse(frame.Payload).RootElement.GetProperty("params").GetProperty("text").GetString() == "GREEDISGOOD", "unchanged request payload");
Check(LobbyTextFrame.TryDecode(Utf8("CONFIRM 71"), out frame) && frame.MessageType == WsWireMessageType.Confirm && frame.TrackingId == "71", "client two-part confirmation accepted");
var unicode = "REQUEST /v2/client/shop/purchase/sign {\"method\":\"/v2/client/shop/purchase/sign\",\"params\":{\"text\":\"Привет мир\"},\"id\":\"72\"}";
Check(LobbyTextFrame.TryDecode(Utf8(unicode), out frame) && frame.Payload.Contains("Привет мир"), "unicode and spaces preserved");
foreach (var bad in new[] { "", "CONFIRM ", "CONFIRM a b", "NOTIFICATION 1 type []", "REQUEST /path", "REQUEST /path nope", "REQUEST /path []", "REQUEST /path {}", "REQUEST /path {\"id\":1,\"method\":\"/path\"}", "REQUEST /path {\"id\":\"\",\"method\":\"/path\"}", "REQUEST /path {\"id\":\"a b\",\"method\":\"/path\"}", "REQUEST /path {\"id\":\"1\",\"method\":\"/other\"}", "REQUEST  {\"id\":\"1\",\"method\":\"\"}", "request /path {}" })
    Check(!LobbyTextFrame.TryDecode(Utf8(bad), out _), "malformed text refused");
Check(!LobbyTextFrame.TryDecode(new byte[] { 0xff, 0xfe }, out _), "invalid utf8 refused");
string[] ClientParse(byte[] value) => Encoding.UTF8.GetString(value).Split(' ', 4, StringSplitOptions.None);
foreach (var kind in new[] { WsWireMessageType.Confirm, WsWireMessageType.Response, WsWireMessageType.Notification })
{
    var payload = kind == WsWireMessageType.Confirm ? "" : "{\"text\":\"words with spaces\"}";
    var method = kind == WsWireMessageType.Confirm ? "" : "/client/mail/msg/send";
    var parts = ClientParse(LobbyTextFrame.Encode(kind, "71", method, payload));
    Check(parts.Length == 4 && parts[1] == "71" && parts[2] == method && parts[3] == payload, "actual client four-part parser accepts reply");
}
foreach (var id in new[] { "", "a b", "a\nb" })
{
    try { LobbyTextFrame.Encode(WsWireMessageType.Response, id, "/path", "{}"); throw new Exception("invalid header accepted"); }
    catch (ArgumentException) { passed++; }
}
Check(LobbyNotificationRoute.For(NotificationEventType.new_message) == "ChatMessageReceived", "chat uses native notification enum header");
Check(LobbyNotificationRoute.For(NotificationEventType.UpdateAccountTarcoinBalance) == "ExpansionsAccountTarcoinBalance", "balance uses native notification enum header");
Check(LobbyNotificationRoute.For(NotificationEventType.UpdateAccountEftBattlePassUniversalDocumentBalance) == "BattlePassUniversalDocuments", "documents use native notification enum header");
Check(LobbyNotificationRoute.For(NotificationEventType.UpdateAccountOfferPurchased) == "ExpansionsAccountOfferPurchased", "purchase uses native notification enum header");
Check(LobbyNotificationRoute.For(null) is null, "unknown notification stays on normal channel");
var channel = new LobbySocketChannel(null!);
using var textSocket = new RecordingSocket();
var session = new MongoId("000000000000000000000001");
channel.Register(session, textSocket);
Check(!await channel.SendNotificationAsync(session, new WsNotificationEvent { EventType = NotificationEventType.NotificationPopup }), "unsupported notification uses existing notifier channel");
await channel.SendFrameAsync(textSocket, WsWireMessageType.Confirm, "71", "", "", CancellationToken.None);
Check(textSocket.Frames.Single().Type == WebSocketMessageType.Text && ClientParse(textSocket.Frames.Single().Data).Length == 4, "registered exact-build channel sends text confirmation");
await channel.SendFrameAsync(textSocket, WsWireMessageType.Response, "71", "/client/mail/msg/send", "{\"id\":\"71\",\"result\":\"\"}", CancellationToken.None);
Check(ClientParse(textSocket.Frames.Last().Data)[0] == "RESPONSE", "text response delivered");
await channel.SendFrameAsync(textSocket, WsWireMessageType.Notification, "72", "ExpansionsAccountTarcoinBalance", "[{\"balance\":500000}]", CancellationToken.None);
Check(ClientParse(textSocket.Frames.Last().Data)[0] == "NOTIFICATION", "text notification delivered");
using var binarySocket = new RecordingSocket();
channel.SetWireFormat(binarySocket, WebSocketMessageType.Binary);
foreach (var kind in new[] { WsWireMessageType.Confirm, WsWireMessageType.Response, WsWireMessageType.Notification })
{
    await channel.SendFrameAsync(binarySocket, kind, "73", "/path", "{\"a\":1}", CancellationToken.None);
    var sent = binarySocket.Frames.Last();
    Check(sent.Type == WebSocketMessageType.Binary && LobbySocketChannel.TryDecodeFrame(sent.Data, out var decoded) && decoded.MessageType == kind && decoded.TrackingId == "73" && decoded.Payload == "{\"a\":1}", "binary path retained");
}
try { channel.SetWireFormat(textSocket, WebSocketMessageType.Close); throw new Exception("invalid wire mode accepted"); }
catch (ArgumentOutOfRangeException) { passed++; }
await Task.WhenAll(Enumerable.Range(0, 16).Select(i => channel.SendFrameAsync(textSocket, WsWireMessageType.Response, (100+i).ToString(), "/path", "{}", CancellationToken.None)));
Check(textSocket.Frames.Count == 19 && textSocket.ConcurrentSends == 0, "concurrent sends serialized");
channel.Unregister(session, textSocket);
Console.WriteLine(JsonSerializer.Serialize(new { passed, failed = 0, networkUsed = false, serverStarted = false, actualClientTextContract = true, binaryRetained = true }));

sealed class RecordingSocket : WebSocket
{
    public readonly List<(WebSocketMessageType Type, byte[] Data)> Frames = [];
    private int _sending;
    public int ConcurrentSends;
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => WebSocketState.Open;
    public override string? SubProtocol => null;
    public override void Abort() { }
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
    public override void Dispose() { }
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _sending) != 1) ConcurrentSends++;
        await Task.Yield();
        Frames.Add((messageType, buffer.ToArray()));
        Interlocked.Decrement(ref _sending);
    }
}
