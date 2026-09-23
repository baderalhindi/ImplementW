namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// How to reach the approved secret store (TASK-019). Both addresses come from the Environment and
/// Secrets sheet; nothing here is read from a checked-in file.
/// </summary>
public sealed class SecretStoreOptions
{
    /// <summary>
    /// <c>SECRET_STORE_ENDPOINT</c> — the base address of this environment's secret namespace,
    /// <em>including the id prefix</em>, e.g.
    /// <c>https://secretmanager.googleapis.com/v1/projects/ahda-pmplatform-dev/secrets/pmplatform-dev-</c>.
    /// </summary>
    /// <remarks>
    /// A variable's address is this string with its lower-kebab name appended, so one value carries the
    /// store, the environment's project and its namespace prefix, and the application needs no variable
    /// the sheet does not name. <c>infra/secrets/inventory.py</c> builds the secret ids in
    /// <c>infra/environments/environments.json</c> by the same rule, and <c>SecretStoreTests</c> asserts
    /// the two agree on every entry.
    /// </remarks>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// <c>SECRET_STORE_AUTH_TOKEN</c> — the bootstrap credential, when the runtime has no platform
    /// identity. Null on Cloud Run, where the token comes from the environment's runtime service account
    /// through the metadata server and no credential is stored anywhere (the sheet: "injected via
    /// deployment platform identity/role, never a static file").
    /// </summary>
    public string? BootstrapToken { get; init; }

    /// <summary>
    /// How often a loaded secret is re-read, so a rotated value reaches a running instance without a
    /// deployment (TASK-019 validation). A delivery-team engineering value, not an AHDA one: the rotation
    /// <em>cadence</em> is the gated value (control matrix G-3, proposed UGV-13).
    /// </summary>
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Per-request timeout against the store.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The variables to load. Defaults to what the application reads today.</summary>
    public IReadOnlyList<string> Keys { get; init; } = ApplicationSecrets.Keys;
}
