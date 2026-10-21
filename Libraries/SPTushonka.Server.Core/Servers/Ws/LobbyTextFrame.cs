using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SPTarkov.Server.Core.Servers.Ws;

/// <summary>Text request frames used by the EFT 1.1 websocket backend contract.</summary>
public static class LobbyTextFrame
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static bool TryDecode(byte[] bytes, out WsWireFrame frame)
    {
        frame = default;
        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException) { return false; }

        if (text.StartsWith("CONFIRM ", StringComparison.Ordinal))
        {
            var id = text[8..];
            if (!Part(id)) return false;
            frame = new WsWireFrame(WsWireMessageType.Confirm, id, string.Empty, string.Empty);
            return true;
        }

        if (!text.StartsWith("REQUEST ", StringComparison.Ordinal)) return false;
        var separator = text.IndexOf(' ', 8);
        if (separator < 0) return false;
        var method = text[8..separator];
        if (!Part(method)) return false;
        var payload = text[(separator + 1)..];
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String
                || methodValue.GetString() != method) return false;
            var id = idValue.GetString();
            if (!Part(id)) return false;
            frame = new WsWireFrame(WsWireMessageType.Request, id!, method, payload);
            return true;
        }
        catch (JsonException) { return false; }
    }

    public static byte[] Encode(WsWireMessageType type, string trackingId, string method, string payload)
    {
        var keyword = type switch
        {
            WsWireMessageType.Confirm => "CONFIRM",
            WsWireMessageType.Response => "RESPONSE",
            WsWireMessageType.Notification => "NOTIFICATION",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        if (!Part(trackingId) || (method.Length > 0 && !Part(method)))
            throw new ArgumentException("Invalid websocket frame header");
        // The client splits incoming data on the first three spaces, preserving the JSON body.
        // A confirmation therefore retains both empty trailing fields.
        return StrictUtf8.GetBytes($"{keyword} {trackingId} {method} {payload}");
    }

    private static bool Part(string? value) => !string.IsNullOrEmpty(value)
        && value.Length <= 4096 && !value.Any(char.IsWhiteSpace) && !value.Any(char.IsControl);
}
