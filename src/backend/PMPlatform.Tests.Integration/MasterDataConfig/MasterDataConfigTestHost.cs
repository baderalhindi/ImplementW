using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.MasterDataConfig;

/// <summary>
/// The identity test host with its own database, and three administrators, because a governed row needs three people
/// (ERD D-12): local.r01 authors, local.r02 reviews and local.r03 publishes, each holding R01's shipped grants.
/// local.r06 (R06) holds no FG-04 permission. Each test works in configuration families no other test publishes in.
/// </summary>
public sealed class MasterDataConfigTestHost : IAsyncLifetime
{
    public const int Author = 1;
    public const int Reviewer = 2;
    public const int Publisher = 3;
    public const int Viewer = 6;

    private static string ReviewerAndPublisherAsAdministrators => $"""
        INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, starts_at, status, created_at, created_by, updated_at, updated_by)
        VALUES ('00000000-0112-4000-8000-000000000002', '{IdentityDatabase.UserId(Reviewer)}', '{IdentityDatabase.ProfileVersionId(1)}', now() - interval '1 day', 'ACTIVE',
                now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'),
               ('00000000-0112-4000-8000-000000000003', '{IdentityDatabase.UserId(Publisher)}', '{IdentityDatabase.ProfileVersionId(1)}', now() - interval '1 day', 'ACTIVE',
                now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
        """;

    public IdentityTestHost Identity { get; } = new();

    public IdentityApiFactory Api => Identity.Api;

    public IdentityDatabase Database => Identity.Database;

    public AdjustableTimeProvider Clock => Identity.Clock;

    public async Task InitializeAsync()
    {
        await Identity.InitializeAsync();
        await Database.ExecuteAsync(ReviewerAndPublisherAsAdministrators);
    }

    public Task DisposeAsync() => Identity.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class MasterDataConfigSuite : ICollectionFixture<MasterDataConfigTestHost>
{
    public const string Name = "MasterDataConfig";
}
