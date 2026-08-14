namespace SPTarkov.Server.Core.Models.Spt.Servers;

public sealed class StreamedJsonBody(object? payload)
{
    public object? Payload { get; } = payload;
}
