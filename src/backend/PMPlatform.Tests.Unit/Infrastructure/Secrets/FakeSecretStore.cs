using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Tests.Unit.Infrastructure.Secrets;

/// <summary>A secret store whose values can be changed between reads, which is what rotation looks like.</summary>
internal sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public int Reads { get; private set; }

    public SecretStoreException? Fault { get; set; }

    public string? this[string name]
    {
        get => _values.GetValueOrDefault(name);
        set => _values[name] = value;
    }

    public Task<string?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        Reads++;
        return Fault is null ? Task.FromResult(_values.GetValueOrDefault(name)) : throw Fault;
    }
}
