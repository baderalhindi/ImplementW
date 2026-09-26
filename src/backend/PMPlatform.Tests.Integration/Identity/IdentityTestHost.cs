namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// One database, one identity provider and one API for the whole identity suite. Tests that move the clock or change a
/// row put it back, so the order they run in does not matter.
/// </summary>
public sealed class IdentityTestHost : IAsyncLifetime
{
    public IdentityDatabase Database { get; } = new();

    public AdjustableTimeProvider Clock { get; } = new();

    public CapturedLogs Logs { get; } = new();

    public TestIdentityProvider IdentityProvider { get; private set; } = null!;

    public IdentityApiFactory Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        IdentityProvider = await TestIdentityProvider.StartAsync();
        Api = CreateApi();
    }

    /// <summary>The API with every identity setting in place, and <paramref name="overrides"/> on top.</summary>
    public IdentityApiFactory CreateApi(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> settings = new(TestDirectory.Settings);
        foreach ((string key, string? value) in IdentityProvider.Settings.Concat(overrides ?? new Dictionary<string, string?>()))
        {
            settings[key] = value;
        }

        settings["DB_CONNECTION_STRING"] = Database.ConnectionString;
        settings.TryAdd("JWT_SIGNING_KEY", IdentityApiFactory.SigningKey);
        return new IdentityApiFactory(settings, Clock, Logs);
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await IdentityProvider.DisposeAsync();
        await Database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IdentitySuite : ICollectionFixture<IdentityTestHost>
{
    public const string Name = "Identity";
}
