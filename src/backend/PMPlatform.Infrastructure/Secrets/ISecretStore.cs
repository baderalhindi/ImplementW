namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// The approved secret-management platform, as the application sees it (TASK-019, CTL-18).
/// </summary>
/// <remarks>
/// One method, taking a Variable Name from the Environment and Secrets sheet. The store's address, the
/// environment's namespace and the credential that opens it are the implementation's business, which is
/// what makes the ADR-001 gate's escape clause — "unless AHDA IT nominates an alternative" — a second
/// implementation of this interface rather than a change to the application.
/// </remarks>
public interface ISecretStore
{
    /// <summary>
    /// The current value of <paramref name="name"/>, or null when the store holds no enabled version of it.
    /// </summary>
    /// <exception cref="SecretStoreException">The store could not be reached or answered an error.</exception>
    public Task<string?> ReadAsync(string name, CancellationToken cancellationToken);
}
