using Microsoft.Extensions.Configuration;

namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// Reads <see cref="SecretStoreOptions.Keys"/> from the approved secret store at start-up and re-reads
/// them on <see cref="SecretStoreOptions.RefreshInterval"/>, so a rotated secret reaches a running
/// instance without a deployment and without a code change (TASK-019 validation check, CTL-18).
/// </summary>
/// <remarks>
/// Registered last, so a secret resolves to the store's value wherever else it is configured. A value read
/// here exists in this provider's dictionary and in the component that asks for it; it is never written to
/// disk and never logged.
/// </remarks>
internal sealed class SecretStoreConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly ISecretStore _store;
    private readonly SecretStoreOptions _options;
    private readonly IDisposable? _owned;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Timer? _refresh;
    private bool _disposed;

    public SecretStoreConfigurationProvider(ISecretStore store, SecretStoreOptions options, IDisposable? owned = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _options = options;
        _owned = owned;
    }

    /// <summary>
    /// The start-up load. A secret that is missing or unreadable fails the application here, by name,
    /// rather than at the first request that needs it (the sheet's verification method: "Application fails
    /// to start with a clear error if unset").
    /// </summary>
    public override void Load()
    {
        ReadAsync(required: true, CancellationToken.None).GetAwaiter().GetResult();

        if (_refresh is null && _options.RefreshInterval > TimeSpan.Zero)
        {
            _refresh = new Timer(_ => Refresh(), state: null, _options.RefreshInterval, _options.RefreshInterval);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refresh?.Dispose();
        _owned?.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// A scheduled re-read. Unlike the start-up load it never throws: a store that is briefly unreachable
    /// leaves the values already loaded in place and the next tick tries again. Nothing relies on a refresh
    /// having succeeded unobserved — the rotation runbook confirms the new value is in effect before the
    /// previous version is disabled (infra/secrets/secret-rotation-runbook.md §4).
    /// </summary>
    private void Refresh()
    {
        try
        {
            ReadAsync(required: false, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (SecretStoreException)
        {
            // Keep serving the values already loaded; the next tick tries again.
        }
    }

    private async Task ReadAsync(bool required, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return; // A read is already in flight; a second would ask the store the same question.
        }

        try
        {
            Dictionary<string, string?> read = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, bool keyRequired) in _options.Keys.Select(k => (k, true)).Concat(_options.OptionalKeys.Select(k => (k, false))))
            {
                string? value = await _store.ReadAsync(key, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(value))
                {
                    if (required && keyRequired)
                    {
                        throw new SecretStoreException(
                            $"{key} has no value in the secret store at {GoogleSecretManagerStore.AddressOf(_options.Endpoint, key)}. " +
                            "The owner named in the Environment and Secrets sheet writes it with infra/secrets/set-secret-version.sh.");
                    }

                    // A version disabled between two reads keeps its last known value rather than emptying
                    // a running instance's configuration.
                    continue;
                }

                read[key] = value;
            }

            if (required)
            {
                Data = read;
                return;
            }

            Dictionary<string, string?> merged = Merge(read);
            if (Differs(merged))
            {
                Data = merged;
                OnReload();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool Differs(Dictionary<string, string?> candidate) =>
        candidate.Count != Data.Count
        || candidate.Any(entry => !Data.TryGetValue(entry.Key, out string? existing)
                                  || !string.Equals(existing, entry.Value, StringComparison.Ordinal));

    private Dictionary<string, string?> Merge(Dictionary<string, string?> read)
    {
        Dictionary<string, string?> merged = new(Data, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string?> entry in read)
        {
            merged[entry.Key] = entry.Value;
        }

        return merged;
    }
}
