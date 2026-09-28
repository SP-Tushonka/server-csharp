using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTarkov.Server.Core.Utils;

[Injectable]
public sealed class HttpFileUtil(HttpServerHelper httpServerHelper)
{
    public async Task SendFileAsync(HttpResponse resp, string filePath, CancellationToken cancellationToken = default)
    {
        var pathSlice = filePath.Split("/");
        var mimePath = httpServerHelper.GetMimeText(pathSlice[^1].Split(".")[^1]);
        var type = string.IsNullOrWhiteSpace(mimePath) ? httpServerHelper.GetMimeText("txt") : mimePath;
        var fileInfo = new FileInfo(filePath);

        // Ranges let a video element fetch only what it plays, and the validators let the browser keep
        // what it already has. The result also treats a download the browser drops as finished.
        var entityTag = new EntityTagHeaderValue($"\"{fileInfo.LastWriteTimeUtc.Ticks:x}-{fileInfo.Length:x}\"");
        await Results
            .File(fileInfo.FullName, type, lastModified: fileInfo.LastWriteTimeUtc, entityTag: entityTag, enableRangeProcessing: true)
            .ExecuteAsync(resp.HttpContext);
    }
}
