namespace SPTarkov.Server.Core.Exceptions.Database;

public sealed class DatabaseModifiedAfterCutoffException : Exception
{
    public DatabaseModifiedAfterCutoffException(string message)
        : base(message) { }

    public DatabaseModifiedAfterCutoffException(string message, Exception innerException)
        : base(message, innerException) { }
}
