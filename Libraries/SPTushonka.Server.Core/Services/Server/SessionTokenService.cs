using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers;

namespace SPTarkov.Server.Core.Services.Server;

[Injectable(InjectionType.Singleton)]
public class SessionTokenService(SaveServer saveServer)
{
    private const string Header = """{"typ":"JWT","alg":"HS256","kid":"eft-backend"}""";

    private readonly byte[] _signingKey = RandomNumberGenerator.GetBytes(32);

    public string IssueToken(MongoId sessionId)
    {
        var aid = saveServer.GetProfile(sessionId)?.ProfileInfo?.Aid ?? 0;

        var claims = new Dictionary<string, object>
        {
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d,
            ["sub"] = aid.ToString(),
            ["ws"] = new Dictionary<string, object> { ["channels"] = new[] { "pve" } },
        };

        var body = $"{Encode(Encoding.UTF8.GetBytes(Header))}.{Encode(JsonSerializer.SerializeToUtf8Bytes(claims))}";

        using var hmac = new HMACSHA256(_signingKey);

        return $"{body}.{Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)))}";
    }

    private static string Encode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
