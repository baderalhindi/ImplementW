using Npgsql;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.MasterDataConfig;

/// <summary>
/// TASK-034 acceptance criterion 1 below the application: the database refuses to rewrite a PUBLISHED version or its
/// content, whoever issues the statement, and lets through only the version's own retirement. Every statement runs in a
/// transaction that is rolled back.
/// </summary>
[Collection(MasterDataConfigSuite.Name)]
public sealed class ConfigurationHistoryGuardTests(MasterDataConfigTestHost host)
{
    private static Guid? _published;

    [Theory]
    [InlineData("UPDATE master_data_config.configuration_value SET value_text = '9' WHERE configuration_version_id = '{0}'")]
    [InlineData("DELETE FROM master_data_config.configuration_value WHERE configuration_version_id = '{0}'")]
    [InlineData("""
        INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{0}', 'ADDED_LATER', '1', 'INTEGER', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
        """)]
    [InlineData("UPDATE master_data_config.configuration_version SET effective_from = effective_from - interval '1 day' WHERE id = '{0}'")]
    [InlineData("UPDATE master_data_config.configuration_version SET lifecycle_state = 'DRAFT', published_at = NULL, effective_from = NULL WHERE id = '{0}'")]
    [InlineData("UPDATE master_data_config.configuration_version SET change_summary = 'Rewritten.' WHERE id = '{0}'")]
    [InlineData("DELETE FROM master_data_config.configuration_version WHERE id = '{0}'")]
    public async Task APublishedVersionAndItsContentAreFixed(string statement)
    {
        Guid version = await PublishedVersionAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(string.Format(null, statement, version)));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
    }

    [Fact]
    public async Task ARetiredVersionTakesNoChangeAtAll()
    {
        Guid version = await PublishedVersionAsync();
        string retire = $"""
            UPDATE master_data_config.configuration_version SET lifecycle_state = 'RETIRED', retired_at = now(), effective_to = now() WHERE id = '{version}';
            UPDATE master_data_config.configuration_version SET effective_to = NULL WHERE id = '{version}';
            """;

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(retire));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        Assert.Contains("RETIRED", refused.MessageText, StringComparison.Ordinal);
    }

    /// <summary>Retirement is the one change a PUBLISHED version takes, and it changes nothing else.</summary>
    [Fact]
    public async Task RetirementIsTheOneChangeAPublishedVersionTakes()
    {
        Guid version = await PublishedVersionAsync();

        IReadOnlyList<string> state = await host.Database.QueryRolledBackAsync(
            $"UPDATE master_data_config.configuration_version SET lifecycle_state = 'RETIRED', retired_at = now(), effective_to = now(), updated_at = now() WHERE id = '{version}'",
            $"SELECT lifecycle_state FROM master_data_config.configuration_version WHERE id = '{version}'");

        Assert.Equal(["RETIRED"], state);
    }

    /// <summary>A publication written past the application must still follow every earlier publication of its family.</summary>
    [Fact]
    public async Task APublicationBeforeAnEarlierOneIsRefused()
    {
        Guid version = await PublishedVersionAsync();
        string backdated = $"""
            INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, published_at, effective_from, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), v.configuration_family_id, 99, 'PUBLISHED', now(), v.effective_from - interval '1 second',
                   now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
            FROM master_data_config.configuration_version v WHERE v.id = '{version}'
            """;

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(backdated));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_configuration_version_effective_from_order"), (refused.SqlState, refused.ConstraintName));
    }

    /// <summary>A DRAFT's content is the author's to change, and an abandoned DRAFT may even be deleted with its rows.</summary>
    [Fact]
    public async Task ADraftAndItsContentStayWritable()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (Guid draft, string eTag) = await client.CreateVersionAsync(crew, "FIELD_CLASSIFICATION");
        using (HttpResponseMessage put = await client.PutAsync(
                   $"{ConfigurationApi.Versions}/{draft}", crew.Author, ConfigurationApi.Update(ConfigurationApi.Values(("MASK_CHARACTER", "TEXT", "*"))), eTag))
        {
            Assert.True(put.IsSuccessStatusCode);
        }

        IReadOnlyList<string> remaining = await host.Database.QueryRolledBackAsync(
            $"""
            UPDATE master_data_config.configuration_value SET value_text = '#' WHERE configuration_version_id = '{draft}';
            DELETE FROM master_data_config.configuration_version WHERE id = '{draft}';
            """,
            $"SELECT count(*)::text FROM master_data_config.configuration_value WHERE configuration_version_id = '{draft}'");

        Assert.Equal(["0"], remaining);
    }

    private async Task<Guid> PublishedVersionAsync()
    {
        if (_published is { } published)
        {
            return published;
        }

        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (Guid version, string eTag) = await client.CreateVersionAsync(crew, "EVIDENCE_POLICY");
        await client.WriteAndPublishAsync(crew, version, eTag, ConfigurationApi.Values(("EVIDENCE_REVIEW_DAYS", "DURATION_DAYS", "10")));
        _published = version;
        return version;
    }
}
