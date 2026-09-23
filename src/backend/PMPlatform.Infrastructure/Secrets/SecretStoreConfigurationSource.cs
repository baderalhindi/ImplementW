using Microsoft.Extensions.Configuration;

namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// The configuration source for <see cref="SecretStoreConfigurationProvider"/>: it owns the HTTP clients
/// the store and the token source are built on, and hands them to the provider to dispose.
/// </summary>
internal sealed class SecretStoreConfigurationSource(SecretStoreOptions options) : IConfigurationSource
{
    private static readonly Uri MetadataServer = new("http://metadata.google.internal/");

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        HttpClient client = new() { Timeout = options.RequestTimeout };
        IAccessTokenSource tokens;
        IDisposable owned;

        if (string.IsNullOrWhiteSpace(options.BootstrapToken))
        {
            HttpClient metadata = new() { BaseAddress = MetadataServer, Timeout = options.RequestTimeout };
            MetadataServerTokenSource source = new(metadata);
            tokens = source;
            owned = new CompositeDisposable(client, metadata, source);
        }
        else
        {
            tokens = new BootstrapTokenSource(options.BootstrapToken);
            owned = client;
        }

        return new SecretStoreConfigurationProvider(new GoogleSecretManagerStore(client, tokens, options), options, owned);
    }

    private sealed class CompositeDisposable(params IDisposable[] parts) : IDisposable
    {
        public void Dispose()
        {
            foreach (IDisposable part in parts)
            {
                part.Dispose();
            }
        }
    }
}
