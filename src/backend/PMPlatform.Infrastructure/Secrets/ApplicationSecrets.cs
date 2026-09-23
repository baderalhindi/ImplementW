namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// The secrets PMPlatform.Api reads (TASK-019). Every entry is a Variable Name from the Environment and
/// Secrets sheet, and the application reads its value from the approved secret store at runtime — never
/// from a checked-in file (CTL-18).
/// </summary>
/// <remarks>
/// A key is added here by the task that first reads it, not in advance: the provider requires every key
/// on this list at start-up, so listing a variable before any code consumes it would refuse to start an
/// environment over a value nothing needs yet. The full per-environment inventory the store holds is
/// <c>infra/environments/environments.json</c>, derived from the sheet by <c>infra/secrets/inventory.py</c>;
/// <c>docs/architecture/secret-management-check.py</c> fails the build if a key here is missing from it.
/// </remarks>
public static class ApplicationSecrets
{
    /// <summary>TASK-014, read by <see cref="Persistence.PostgreSqlHealthCheck"/> and by the data tier.</summary>
    public const string DatabaseConnectionString = "DB_CONNECTION_STRING";

    /// <summary>The configuration keys loaded from the secret store, in the order they are read.</summary>
    public static readonly IReadOnlyList<string> Keys = [DatabaseConnectionString];
}
