using Microsoft.Extensions.Configuration;

namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// Wires the approved secret store into the application's configuration (TASK-019).
/// </summary>
public static class SecretStoreConfigurationExtensions
{
    /// <summary>
    /// Adds the secret store as the last configuration source, addressed by the two bootstrap variables
    /// the Environment and Secrets sheet names: <c>SECRET_STORE_ENDPOINT</c> and, where the runtime has no
    /// platform identity, <c>SECRET_STORE_AUTH_TOKEN</c>.
    /// </summary>
    /// <param name="builder">The application's configuration builder.</param>
    /// <param name="required">
    /// Whether an environment without <c>SECRET_STORE_ENDPOINT</c> is a failure. True in every deployed
    /// environment; false only for local development, where TASK-013's git-ignored
    /// <c>appsettings.Development.Local.json</c> stands in for the store and no AHDA secret exists.
    /// </param>
    /// <exception cref="SecretStoreException">
    /// <paramref name="required"/> is true and no usable endpoint is configured. Failing here is the point:
    /// the alternative is an environment that silently serves whatever a checked-in file happens to hold.
    /// </exception>
    public static IConfigurationBuilder AddSecretStore(this IConfigurationBuilder builder, bool required)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationRoot bootstrap = builder.Build();
        string? endpoint = bootstrap["SECRET_STORE_ENDPOINT"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return required
                ? throw new SecretStoreException(
                    "SECRET_STORE_ENDPOINT is not set. Every deployed environment reads its secrets from the " +
                    "approved secret store; only local development may run without it.")
                : builder;
        }

        Uri address = Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsed)
            ? parsed
            : throw new SecretStoreException($"SECRET_STORE_ENDPOINT is not an absolute URI: {endpoint}");

        return builder.Add(new SecretStoreConfigurationSource(new SecretStoreOptions
        {
            Endpoint = address,
            BootstrapToken = bootstrap["SECRET_STORE_AUTH_TOKEN"],
        }));
    }
}
