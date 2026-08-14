using System.Buffers;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Servers;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Servers.Http;

[Injectable]
public class SptHttpListener(
    HttpRouter httpRouter,
    IEnumerable<ISerializer> serializers,
    ISptLogger<SptHttpListener> logger,
    ISptLogger<RequestLogger> requestsLogger,
    JsonUtil jsonUtil,
    HttpResponseUtil httpResponseUtil,
    HttpConfig? httpConfig = null
) : IHttpListener
{
    private static readonly ImmutableHashSet<string> _supportedMethods = ["GET", "PUT", "POST"];

    private const int ChunkChars = 16 * 1024;

    private bool RequestLoggingEnabled
    {
        get { return (httpConfig?.LogRequests ?? true) && ProgramStatics.ENTRY_TYPE() != EntryType.RELEASE; }
    }

    private bool ResponseBodyLoggingEnabled
    {
        get { return RequestLoggingEnabled && (httpConfig?.LogResponseBodies ?? false); }
    }

    public bool CanHandle(HttpContext context)
    {
        return _supportedMethods.Contains(context.Request.Method) && httpRouter.CanHandle(context);
    }

    public async Task HandleAsync(MongoId sessionId, HttpContext context, CancellationToken cancellationToken = default)
    {
        switch (context.Request.Method)
        {
            case "GET":
            {
                var response = await GetResponseObjectAsync(sessionId, context, null, cancellationToken);

                // Another handler is already handling this, or no handler was found.
                if (response is null)
                {
                    return;
                }

                await SendResponseAsync(sessionId, context.Request, context.Response, null, response, cancellationToken);
                break;
            }
            // these are handled almost identically.
            case "POST":
            case "PUT":
            {
                // Contrary to reasonable expectations, the content-encoding is _not_ actually used to
                // determine if the payload is compressed. All PUT requests are, and POST requests without
                // debug = 1 are as well. This should be fixed.
                // let compressed = req.headers["content-encoding"] === "deflate";
                var requestIsCompressed =
                    !context.Request.Headers.TryGetValue("requestcompressed", out var compressHeader) || compressHeader != "0";
                var requestCompressed = context.Request.Method == "PUT" || requestIsCompressed;

                string body;

                if (requestCompressed)
                {
                    await using var deflateStream = new ZLibStream(context.Request.Body, CompressionMode.Decompress);
                    using var reader = new StreamReader(deflateStream, Encoding.UTF8);
                    body = await reader.ReadToEndAsync(cancellationToken);
                }
                else
                {
                    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
                    body = await reader.ReadToEndAsync(cancellationToken);
                }

                if (!requestIsCompressed)
                {
                    if (logger.IsLogEnabled(LogLevel.Debug))
                    {
                        logger.Debug(body);
                    }
                }

                var response = await GetResponseObjectAsync(sessionId, context, body, cancellationToken);

                // Another handler is already handling this, or no handler was found.
                if (response is null)
                {
                    return;
                }

                await SendResponseAsync(sessionId, context.Request, context.Response, body, response, cancellationToken);
                break;
            }
        }
    }

    public async Task SendResponseAsync(
        MongoId sessionID,
        HttpRequest req,
        HttpResponse resp,
        object? body,
        object output,
        CancellationToken cancellationToken = default
    )
    {
        if (output is StreamedJsonBody streamed)
        {
            await SendStreamedJsonAsync(resp, streamed, sessionID, IsDebugRequest(req), cancellationToken);
            LogStreamedRequest(req, streamed);

            return;
        }

        await SendResponseAsync(sessionID, req, resp, body, (string)output, cancellationToken);
    }

    /// <summary>
    ///     Send HTTP response back to sender
    /// </summary>
    /// <param name="sessionID"> Player id making request </param>
    /// <param name="req"> Incoming request </param>
    /// <param name="resp"> Outgoing response </param>
    /// <param name="body"> Buffer </param>
    /// <param name="output"> Server generated response data</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that can be used to cancel the response operation.
    /// </param>
    public async Task SendResponseAsync(
        MongoId sessionID,
        HttpRequest req,
        HttpResponse resp,
        object? body,
        string output,
        CancellationToken cancellationToken = default
    )
    {
        if (IsDebugRequest(req))
        {
            // Send only raw response without transformation
            await SendJsonAsync(resp, output, sessionID, cancellationToken);
            if (logger.IsLogEnabled(LogLevel.Debug))
            {
                logger.Debug($"Response: {output}");
            }

            LogRequest(req, output);
            return;
        }

        // Not debug, minority of requests need a serializer to do the job (IMAGE/BUNDLE/NOTIFY)
        var serialiser = serializers.FirstOrDefault(x => x.CanHandle(output));
        if (serialiser != null)
        {
            await serialiser.SerializeAsync(sessionID, req, resp, jsonUtil.Serialize(body ?? new object()), cancellationToken);
        }
        else
        // No serializer can handle the request (majority of requests don't), zlib the output and send response back
        {
            await SendZlibJsonAsync(resp, output, sessionID, cancellationToken);
        }

        LogRequest(req, output);
    }

    /// <summary>
    ///     Is request flagged as debug enabled
    /// </summary>
    /// <param name="req"> Incoming request </param>
    /// <returns> True if request is flagged as debug </returns>
    protected bool IsDebugRequest(HttpRequest req)
    {
        return req.Headers.TryGetValue("responsecompressed", out var value) && value == "0";
    }

    /// <summary>
    ///     Log request if enabled
    /// </summary>
    /// <param name="req"> Log request if enabled </param>
    /// <param name="output"> Output string </param>
    protected void LogRequest(HttpRequest req, string output)
    {
        if (!RequestLoggingEnabled)
        {
            return;
        }

        // Logging these can get really large, not something we want to do even in debug without the user wanting it to happen
        var body = ResponseBodyLoggingEnabled ? output : $"[{output.Length} chars]";

        requestsLogger.Info($"RESPONSE={jsonUtil.Serialize(new Response(req.Method, body))}");
    }

    /// <summary>
    ///     Log request if enabled
    /// </summary>
    /// <param name="req"> Log request if enabled </param>
    /// <param name="streamed"> streamed data </param>
    private void LogStreamedRequest(HttpRequest req, StreamedJsonBody streamed)
    {
        if (!RequestLoggingEnabled)
        {
            return;
        }

        // Logging these can get really large, not something we want to do even in debug without the user wanting it to happen
        var body = ResponseBodyLoggingEnabled ? jsonUtil.Serialize(streamed.Payload) : "[streamed]";

        requestsLogger.Info($"RESPONSE={jsonUtil.Serialize(new Response(req.Method, body))}");
    }

    private async Task SendStreamedJsonAsync(
        HttpResponse resp,
        StreamedJsonBody streamed,
        MongoId sessionID,
        bool uncompressed,
        CancellationToken cancellationToken
    )
    {
        resp.StatusCode = 200;
        resp.ContentType = "application/json";
        resp.Headers.Append("Set-Cookie", $"PHPSESSID={sessionID.ToString()}");

        if (uncompressed)
        {
            await JsonSerializer.SerializeAsync(resp.Body, streamed.Payload, JsonUtil.JsonSerializerOptionsNoIndent!, cancellationToken);

            return;
        }

        await using var deflateStream = new ZLibStream(resp.Body, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(deflateStream, streamed.Payload, JsonUtil.JsonSerializerOptionsNoIndent!, cancellationToken);
    }

    public async ValueTask<object> GetResponseObjectAsync(
        MongoId sessionId,
        HttpContext context,
        string? body,
        CancellationToken cancellationToken = default
    )
    {
        var output = await httpRouter.GetResponseObjectAsync(context.Request, sessionId, body, cancellationToken);

        // Route doesn't exist or response is not properly set up
        if (output is not StreamedJsonBody && string.IsNullOrEmpty(output as string))
        {
            output = httpResponseUtil.GetBody<object?>(
                null,
                BackendErrorCodes.HTTPNotFound,
                $"UNHANDLED RESPONSE: {context.Request.Path.ToString()}"
            );
        }

        if (RequestLoggingEnabled)
        {
            // Parse quest info into object
            var log = new Request(context.Request.Method, new RequestData(context.Request.Path.ToString(), context.Request.Headers));
            requestsLogger.Info($"REQUEST={jsonUtil.Serialize(log)}");
        }

        return output;
    }

    public async Task SendJsonAsync(HttpResponse resp, string? output, MongoId sessionID, CancellationToken cancellationToken = default)
    {
        resp.StatusCode = 200;
        resp.ContentType = "application/json";
        resp.Headers.Append("Set-Cookie", $"PHPSESSID={sessionID.ToString()}");

        if (!string.IsNullOrEmpty(output))
        {
            await resp.WriteAsync(output, cancellationToken: cancellationToken);
        }
    }

    public async Task SendZlibJsonAsync(HttpResponse resp, string output, MongoId sessionID, CancellationToken cancellationToken = default)
    {
        resp.StatusCode = 200;
        resp.ContentType = "application/json";
        resp.Headers.Append("Set-Cookie", $"PHPSESSID={sessionID.ToString()}");

        await using var deflateStream = new ZLibStream(resp.Body, CompressionLevel.SmallestSize);

        var encoder = Encoding.UTF8.GetEncoder();
        var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(ChunkChars));
        try
        {
            for (var offset = 0; offset < output.Length; offset += ChunkChars)
            {
                var take = Math.Min(ChunkChars, output.Length - offset);
                var written = encoder.GetBytes(output.AsSpan(offset, take), buffer, offset + take == output.Length);

                await deflateStream.WriteAsync(buffer.AsMemory(0, written), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private record Response(string Method, string jsonData);

    private record Request(string Method, object output);

    private record RequestData(string Url, object Headers);
}
