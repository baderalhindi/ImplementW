namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// A secret could not be read from the approved secret store. The message names the variable and the
/// address it was read from; it never carries a secret value, because an exception message reaches logs
/// and error trackers (CTL-18: no secret in logs).
/// </summary>
public sealed class SecretStoreException : Exception
{
    public SecretStoreException()
    {
    }

    public SecretStoreException(string message)
        : base(message)
    {
    }

    public SecretStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
