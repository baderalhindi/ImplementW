using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Tests.Unit.Infrastructure.Secrets;

/// <summary>
/// The secret-store integration (TASK-019, CTL-18): where a variable is read from, what happens when it is
/// not set, and that a rotated value reaches a running application without a restart or a code change.
/// </summary>
public sealed class SecretStoreTests
{
    private static readonly Uri Endpoint =
        new("https://secretmanager.googleapis.com/v1/projects/ahda-pmplatform-dev/secrets/pmplatform-dev-");

    [Fact]
    public void AVariableIsReadFromItsIdUnderTheConfiguredNamespace()
    {
        Uri address = GoogleSecretManagerStore.AddressOf(Endpoint, "DB_CONNECTION_STRING");

        Assert.Equal(
            "https://secretmanager.googleapis.com/v1/projects/ahda-pmplatform-dev/secrets/"
            + "pmplatform-dev-db-connection-string/versions/latest:access",
            address.ToString());
    }

    /// <summary>
    /// The id rule exists twice — here and in infra/secrets/inventory.py, which writes the ids into
    /// infra/environments/environments.json. This is what keeps the two from drifting: every id the
    /// manifest holds must be the one the application will ask for.
    /// </summary>
    [Fact]
    public void EverySecretIdInTheManifestIsTheOneTheApplicationAsksFor()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(ManifestPath()));
        int checkedIds = 0;

        foreach (JsonElement environment in manifest.RootElement.GetProperty("environments").EnumerateArray())
        {
            string prefix = environment.GetProperty("secret_store").GetProperty("prefix").GetString()!;
            foreach (JsonProperty variable in environment.GetProperty("variables").EnumerateObject())
            {
                if (variable.Value.GetProperty("kind").GetString() != "secret")
                {
                    continue;
                }

                Assert.Equal(
                    variable.Value.GetProperty("secret_id").GetString(),
                    prefix + GoogleSecretManagerStore.SecretId(variable.Name));
                checkedIds++;
            }
        }

        Assert.Equal(69, checkedIds);
    }

    [Fact]
    public async Task AVariableWithNoEnabledVersionReadsAsUnset()
    {
        using StubHandler handler = new(HttpStatusCode.NotFound, "{}");
        using HttpClient client = new(handler);
        GoogleSecretManagerStore store = new(client, new BootstrapTokenSource("bootstrap"), Options());

        Assert.Null(await store.ReadAsync("DB_CONNECTION_STRING", CancellationToken.None));
    }

    [Fact]
    public async Task AReadIsAuthorisedWithTheBootstrapCredential()
    {
        using StubHandler handler = new(HttpStatusCode.OK, Payload("Host=db;Password=rotated"));
        using HttpClient client = new(handler);
        GoogleSecretManagerStore store = new(client, new BootstrapTokenSource("bootstrap"), Options());

        string? value = await store.ReadAsync("DB_CONNECTION_STRING", CancellationToken.None);

        Assert.Equal("Host=db;Password=rotated", value);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("bootstrap", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    /// <summary>CTL-18: no secret in logs. An exception message reaches both.</summary>
    [Fact]
    public async Task AFailedReadNamesTheVariableAndNotItsValue()
    {
        using StubHandler handler = new(HttpStatusCode.Forbidden, Payload("Host=db;Password=secret"));
        using HttpClient client = new(handler);
        GoogleSecretManagerStore store = new(client, new BootstrapTokenSource("bootstrap"), Options());

        SecretStoreException failure = await Assert.ThrowsAsync<SecretStoreException>(
            () => store.ReadAsync("DB_CONNECTION_STRING", CancellationToken.None));

        Assert.Contains("DB_CONNECTION_STRING", failure.Message, StringComparison.Ordinal);
        Assert.Contains("403", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=secret", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnApplicationWhoseSecretIsUnsetDoesNotStart()
    {
        FakeSecretStore store = new();
        using SecretStoreConfigurationProvider provider = new(store, Options());

        SecretStoreException failure = Assert.Throws<SecretStoreException>(provider.Load);

        Assert.Contains(ApplicationSecrets.DatabaseConnectionString, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// TASK-028: the directory and SSO secrets exist only where the sheet scopes them (AD_* are not in DEV). An environment
    /// without one starts, with that sign-in method unconfigured; an environment with one reads it.
    /// </summary>
    [Fact]
    public void AnUnsetOptionalSecretDoesNotStopStartUp()
    {
        FakeSecretStore store = new() { [ApplicationSecrets.DatabaseConnectionString] = "Host=db" };
        using SecretStoreConfigurationProvider provider = new(store, Options());

        provider.Load();

        Assert.False(provider.TryGet(ApplicationSecrets.DirectoryBindPassword, out _));
    }

    [Fact]
    public void ASetOptionalSecretIsRead()
    {
        FakeSecretStore store = new()
        {
            [ApplicationSecrets.DatabaseConnectionString] = "Host=db",
            [ApplicationSecrets.DirectoryBindPassword] = "service-account-password",
        };
        using SecretStoreConfigurationProvider provider = new(store, Options());

        provider.Load();

        Assert.True(provider.TryGet(ApplicationSecrets.DirectoryBindPassword, out string? value));
        Assert.Equal("service-account-password", value);
    }

    /// <summary>
    /// The TASK-019 validation check, at the level it can be asserted without a provisioned environment:
    /// the value changes in the store and the running configuration follows it, with no restart, no
    /// redeployment and no code change.
    /// </summary>
    [Fact]
    public async Task ARotatedSecretReachesARunningApplication()
    {
        FakeSecretStore store = new() { [ApplicationSecrets.DatabaseConnectionString] = "Host=db;Password=old" };
        using SecretStoreConfigurationProvider provider = new(
            store,
            Options(refreshInterval: TimeSpan.FromMilliseconds(20)));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .Add(new StubSource(provider))
            .Build();

        Assert.Equal("Host=db;Password=old", configuration[ApplicationSecrets.DatabaseConnectionString]);

        store[ApplicationSecrets.DatabaseConnectionString] = "Host=db;Password=new";

        Assert.True(
            await Eventually(() => configuration[ApplicationSecrets.DatabaseConnectionString] == "Host=db;Password=new"),
            "the rotated value did not reach the running configuration");
    }

    /// <summary>A store that is briefly unreachable leaves the loaded values in place.</summary>
    [Fact]
    public async Task AFailedRefreshKeepsTheValueAlreadyInUse()
    {
        FakeSecretStore store = new() { [ApplicationSecrets.DatabaseConnectionString] = "Host=db;Password=old" };
        using SecretStoreConfigurationProvider provider = new(
            store,
            Options(refreshInterval: TimeSpan.FromMilliseconds(20)));
        provider.Load();

        int before = store.Reads;
        store.Fault = new SecretStoreException("the store is unreachable");

        Assert.True(await Eventually(() => store.Reads > before), "the provider stopped re-reading the store");
        Assert.True(provider.TryGet(ApplicationSecrets.DatabaseConnectionString, out string? value));
        Assert.Equal("Host=db;Password=old", value);
    }

    /// <summary>
    /// One required key and one optional key, whatever the application lists today, so each test is about the
    /// behaviour and not about the current inventory.
    /// </summary>
    private static SecretStoreOptions Options(TimeSpan? refreshInterval = null) => new()
    {
        Endpoint = Endpoint,
        RefreshInterval = refreshInterval ?? TimeSpan.Zero,
        Keys = [ApplicationSecrets.DatabaseConnectionString],
        OptionalKeys = [ApplicationSecrets.DirectoryBindPassword],
    };

    private static string Payload(string value)
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return $$"""{"payload": {"data": "{{encoded}}"} }""";
    }

    private static async Task<bool> Eventually(Func<bool> condition)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20, CancellationToken.None);
        }

        return condition();
    }

    /// <summary>Walks up from the test binaries to the repository, which holds the manifest.</summary>
    private static string ManifestPath()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "infra", "environments", "environments.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("infra/environments/environments.json was not found above the test binaries.");
    }

    private sealed class StubSource(IConfigurationProvider provider) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => provider;
    }
}
