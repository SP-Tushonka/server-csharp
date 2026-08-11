namespace SPTarkov.Server.Exceptions;

public sealed class OutdatedModException(string modName, string reason, Exception innerException)
    : Exception($"Mod `{modName}` was built for a different version of SPT and was not loaded. Reason: {reason}", innerException)
{
    public string ModName { get; } = modName;
}
