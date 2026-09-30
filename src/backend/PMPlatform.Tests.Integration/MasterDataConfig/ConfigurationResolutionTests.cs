using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.MasterDataConfig;

/// <summary>
/// TASK-034's acceptance criteria and validation checks through the API and the database: publishing never rewrites a
/// published version; a past date resolves to the version effective then; missing configuration fails the operation.
/// </summary>
[Collection(MasterDataConfigSuite.Name)]
public sealed class ConfigurationResolutionTests(MasterDataConfigTestHost host) : IDisposable
{

    public void Dispose() => host.Clock.Reset();

    /// <summary>
    /// The workbook's validation check: publish two successive versions and resolve "as of" a date before the second
    /// publish; it returns the first version's value. Acceptance criterion 1 on the way: the first version's row and
    /// content are byte-for-byte what they were before the second was published.
    /// </summary>
    [Fact]
    public async Task ASecondPublicationLeavesTheFirstUntouchedAndAPastDateStillResolvesToIt()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        DateTimeOffset firstPublished = host.Clock.GetUtcNow();
        (Guid first, string firstETag) = await client.CreateVersionAsync(crew, "WORKFLOW_POLICY");
        await client.WriteAndPublishAsync(crew, first, firstETag, ConfigurationApi.Values(("REMINDER_OFFSET_DAYS", "DURATION_DAYS", "5")));
        IReadOnlyList<string> firstBefore = await host.Database.QueryAsync(SnapshotOf(first));

        host.Clock.Advance(TimeSpan.FromHours(1));
        crew = await client.SignInCrewAsync();
        (Guid second, string secondETag) = await client.CreateVersionAsync(crew, "WORKFLOW_POLICY", basedOnVersionId: first);
        using (HttpResponseMessage copied = await client.GetAsync($"{ConfigurationApi.Versions}/{second}", crew.Author))
        {
            Assert.Equal("5", (await copied.ReadObjectAsync())["content"]!["values"]![0]!["value"]!.GetValue<string>());
        }

        await client.WriteAndPublishAsync(crew, second, secondETag, ConfigurationApi.Values(("REMINDER_OFFSET_DAYS", "DURATION_DAYS", "9")));

        Assert.Equal(firstBefore, await host.Database.QueryAsync(SnapshotOf(first)));
        JsonObject past = await ResolveAsync(client, crew.Author, "WORKFLOW_POLICY", firstPublished.AddMinutes(30));
        JsonObject now = await ResolveAsync(client, crew.Author, "WORKFLOW_POLICY", null);
        Assert.Equal((1, "5"), (past["versionNo"]!.GetValue<int>(), past["content"]!["values"]![0]!["value"]!.GetValue<string>()));
        Assert.Equal((2, "9"), (now["versionNo"]!.GetValue<int>(), now["content"]!["values"]![0]!["value"]!.GetValue<string>()));
        Assert.Equal("SUPERSEDED", await EffectivityAsync(client, crew.Author, first));
        Assert.Equal("ACTIVE", await EffectivityAsync(client, crew.Author, second));

        // What a module sees in process (E-U2): the value in force on the transaction date, read as its type.
        using IServiceScope scope = host.Api.Services.CreateScope();
        IConfigurationResolver resolver = scope.ServiceProvider.GetRequiredService<IConfigurationResolver>();
        Assert.Equal(5, (await resolver.ResolveAsync("WORKFLOW_POLICY", firstPublished.AddMinutes(30), CancellationToken.None)).RequireDurationDays("REMINDER_OFFSET_DAYS"));
        Assert.Equal(9, (await resolver.ResolveAsync("WORKFLOW_POLICY", host.Clock.GetUtcNow(), CancellationToken.None)).RequireDurationDays("REMINDER_OFFSET_DAYS"));
        Assert.Equal(5, (await resolver.ResolvePinnedAsync(first, CancellationToken.None)).RequireDurationDays("REMINDER_OFFSET_DAYS"));
    }

    /// <summary>
    /// The workbook's validation check: with required configuration missing, the dependent operation fails explicitly —
    /// 422 CONFIGURATION_MISSING, or an exception in process — and nothing is defaulted.
    /// </summary>
    [Fact]
    public async Task MissingConfigurationFailsTheOperationExplicitly()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();

        using (HttpResponseMessage noVersion = await client.GetAsync(ConfigurationApi.Resolution("KPI_POLICY"), crew.Author))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, noVersion.StatusCode);
            Assert.Equal("application/problem+json", noVersion.Content.Headers.ContentType!.MediaType);
            Assert.Equal("CONFIGURATION_MISSING", await noVersion.CodeOfAsync());
        }

        using (HttpResponseMessage noFamily = await client.GetAsync(ConfigurationApi.Resolution("NOT_A_FAMILY"), crew.Author))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "CONFIGURATION_MISSING"), (noFamily.StatusCode, await noFamily.CodeOfAsync()));
        }

        (Guid version, string eTag) = await client.CreateVersionAsync(crew, "REPORT_RULES");
        await client.WriteAndPublishAsync(crew, version, eTag, ConfigurationApi.Values(("EXPORT_ROW_LIMIT", "INTEGER", "1000")));
        using IServiceScope scope = host.Api.Services.CreateScope();
        IConfigurationResolver resolver = scope.ServiceProvider.GetRequiredService<IConfigurationResolver>();
        ResolvedConfiguration resolved = await resolver.ResolveAsync("REPORT_RULES", host.Clock.GetUtcNow(), CancellationToken.None);

        Assert.Equal(1000, resolved.RequireInteger("EXPORT_ROW_LIMIT"));
        ConfigurationMissingException missing = Assert.Throws<ConfigurationMissingException>(() => resolved.RequireInteger("EXPORT_TIMEOUT_SECONDS"));
        Assert.Equal(ConfigurationMissingReason.EntryMissing, missing.Reason);
        await Assert.ThrowsAsync<ConfigurationMissingException>(() => resolver.ResolveAsync("REPORT_RULES", host.Clock.GetUtcNow().AddYears(-1), CancellationToken.None));
        await Assert.ThrowsAsync<ConfigurationMissingException>(() => resolver.ResolvePinnedAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>
    /// ERD D-12 through the API: only the author edits the draft, the reviewer is someone else, and the publisher is a
    /// third person. A publication takes effect now or later, never in the past, and a PUBLISHED version takes no edit.
    /// </summary>
    [Fact]
    public async Task AVersionNeedsThreePeopleAndCannotBeBackdatedOrEdited()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (Guid version, string eTag) = await client.CreateVersionAsync(crew, "PARTICIPATION");
        object content = ConfigurationApi.Values(("CONTRIBUTION_REVIEW_DAYS", "DURATION_DAYS", "3"));

        await AssertRefusedAsync("MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES", client.PutAsync($"{ConfigurationApi.Versions}/{version}", crew.Reviewer, ConfigurationApi.Update(content), eTag));
        using (HttpResponseMessage put = await client.PutAsync($"{ConfigurationApi.Versions}/{version}", crew.Author, ConfigurationApi.Update(content), eTag))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            eTag = AdministrationApi.ETagOf(put);
        }

        await AssertRefusedAsync("MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES", client.PostAsync($"{ConfigurationApi.Versions}/{version}/validate", crew.Author));
        using (HttpResponseMessage validate = await client.PostAsync($"{ConfigurationApi.Versions}/{version}/validate", crew.Reviewer))
        {
            Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        }

        await AssertRefusedAsync("MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES", client.PostAsync($"{ConfigurationApi.Versions}/{version}/publish", crew.Author));
        await AssertRefusedAsync("MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES", client.PostAsync($"{ConfigurationApi.Versions}/{version}/publish", crew.Reviewer));
        await AssertRefusedAsync("MASTER_DATA_CONFIG_EFFECTIVE_FROM_INVALID",
            client.PostAsync($"{ConfigurationApi.Versions}/{version}/publish", crew.Publisher, new { effectiveFrom = host.Clock.GetUtcNow().AddDays(-1) }));
        using (HttpResponseMessage publish = await client.PostAsync($"{ConfigurationApi.Versions}/{version}/publish", crew.Publisher))
        {
            Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
            JsonObject published = await publish.ReadObjectAsync();
            Assert.Equal(
                ("PUBLISHED", IdentityDatabase.UserId(1), IdentityDatabase.UserId(2), IdentityDatabase.UserId(3)),
                (published["governance"]!["lifecycleState"]!.GetValue<string>(), published["governance"]!["createdBy"]!.GetValue<string>(),
                 published["governance"]!["validatedByUserId"]!.GetValue<string>(), published["governance"]!["publishedByUserId"]!.GetValue<string>()));
        }

        using HttpResponseMessage current = await client.GetAsync($"{ConfigurationApi.Versions}/{version}", crew.Author);
        using HttpResponseMessage edit = await client.PutAsync($"{ConfigurationApi.Versions}/{version}", crew.Author, ConfigurationApi.Update(content), AdministrationApi.ETagOf(current));
        Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), (edit.StatusCode, await edit.CodeOfAsync()));
    }

    /// <summary>
    /// FUTURE_EFFECTIVE → ACTIVE by date alone, and a withdrawal before the date means the version never resolves: the
    /// version in force before it stays in force.
    /// </summary>
    [Fact]
    public async Task AFutureVersionTakesEffectOnItsDateAndAWithdrawnOneNever()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (Guid current, string currentETag) = await client.CreateVersionAsync(crew, "DASHBOARD_RULES");
        await client.WriteAndPublishAsync(crew, current, currentETag, ConfigurationApi.Values(("DATA_FRESHNESS_HOURS", "INTEGER", "24")));
        DateTimeOffset nextWeek = host.Clock.GetUtcNow().AddDays(7);
        (Guid future, string futureETag) = await client.CreateVersionAsync(crew, "DASHBOARD_RULES", current);
        JsonObject published = await client.WriteAndPublishAsync(
            crew, future, futureETag, ConfigurationApi.Values(("DATA_FRESHNESS_HOURS", "INTEGER", "12")), new { effectiveFrom = nextWeek });

        Assert.Equal("FUTURE_EFFECTIVE", published["effectivity"]!.GetValue<string>());
        Assert.Equal(1, (await ResolveAsync(client, crew.Author, "DASHBOARD_RULES", null))["versionNo"]!.GetValue<int>());
        Assert.Equal(2, (await ResolveAsync(client, crew.Author, "DASHBOARD_RULES", nextWeek))["versionNo"]!.GetValue<int>());

        using HttpResponseMessage retire = await client.PostAsync($"{ConfigurationApi.Versions}/{future}/retire", crew.Publisher);
        JsonObject retired = await retire.ReadObjectAsync();

        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);
        Assert.Equal(("RETIRED", published["effectiveFrom"]!.GetValue<DateTimeOffset>()), (retired["effectivity"]!.GetValue<string>(), retired["effectiveTo"]!.GetValue<DateTimeOffset>()));
        Assert.Equal(1, (await ResolveAsync(client, crew.Author, "DASHBOARD_RULES", nextWeek.AddDays(1)))["versionNo"]!.GetValue<int>());
        using HttpResponseMessage again = await client.PostAsync($"{ConfigurationApi.Versions}/{future}/retire", crew.Publisher);
        Assert.Equal((HttpStatusCode.Conflict, "TERMINAL_STATE"), (again.StatusCode, await again.CodeOfAsync()));
    }

    /// <summary>
    /// ADR-011 and the gate decision "publish the schema, seed later": the seeded RISK_MATRIX draft (generic levels, no
    /// ratings or cells) cannot be validated; completed with ratings and the 25 cells, it publishes and rates a risk.
    /// </summary>
    [Fact]
    public async Task TheSeededRiskScaleNeedsItsRatingsAndCellsBeforeItPublishes()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        Guid seeded = Guid.Parse((await host.Database.QueryAsync("SELECT md5('configuration_version:RISK_MATRIX:1')")).Single());
        (Guid version, _) = await client.CreateVersionAsync(crew, "RISK_MATRIX", basedOnVersionId: seeded);

        using (HttpResponseMessage incomplete = await client.PostAsync($"{ConfigurationApi.Versions}/{version}/validate", crew.Reviewer))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "MASTER_DATA_CONFIG_CONTENT_INCOMPLETE"), (incomplete.StatusCode, await incomplete.CodeOfAsync()));
            Assert.Equal(["content.riskRatings INCOMPLETE", "content.riskMatrixCells INCOMPLETE"], await incomplete.ReadFieldErrorsAsync());
        }

        using HttpResponseMessage read = await client.GetAsync($"{ConfigurationApi.Versions}/{version}", crew.Author);
        JsonObject content = (await read.ReadObjectAsync())["content"]!.AsObject();
        Assert.Equal((5, 20), (content["probabilityLevels"]!.AsArray().Count, content["impactLevels"]!.AsArray().Count));
        content["riskRatings"] = new JsonArray(
            new JsonObject { ["code"] = "LOW", ["label"] = new JsonObject { ["ar"] = "منخفض", ["en"] = "Low" }, ["sortOrder"] = 1 },
            new JsonObject { ["code"] = "HIGH", ["label"] = new JsonObject { ["ar"] = "مرتفع", ["en"] = "High" }, ["sortOrder"] = 2 });
        content["riskMatrixCells"] = new JsonArray([
            .. Enumerable.Range(1, 5).SelectMany(p => Enumerable.Range(1, 5).Select(i =>
                (JsonNode)new JsonObject { ["probabilityLevel"] = p, ["impactLevel"] = i, ["ratingCode"] = p * i >= 12 ? "HIGH" : "LOW" })),
        ]);

        await client.WriteAndPublishAsync(crew, version, AdministrationApi.ETagOf(read), content.DeepClone());

        using IServiceScope scope = host.Api.Services.CreateScope();
        ResolvedConfiguration matrix = await scope.ServiceProvider.GetRequiredService<IConfigurationResolver>()
            .ResolveAsync("RISK_MATRIX", host.Clock.GetUtcNow(), CancellationToken.None);
        Assert.Equal(("HIGH", "LOW"), (matrix.RequireRiskRating(3, 4).Code, matrix.RequireRiskRating(2, 5).Code));
    }

    /// <summary>
    /// ADR-004: the event family → channel, recipient role → event family and Mandatory/User-configurable matrices are
    /// published configuration, so the SMS event set changes without a release.
    /// </summary>
    [Fact]
    public async Task TheNotificationMatricesArePublishedConfiguration()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (Guid version, string eTag) = await client.CreateVersionAsync(crew, "NOTIFICATION_ROUTING");
        object securityAlert = EventFamily("SECURITY_ALERT", ("IN_APP", true), ("SMS", true));
        object mandatoryWithoutChannel = EventFamily("ESCALATION", ("EMAIL", false));

        using (HttpResponseMessage put = await client.PutAsync($"{ConfigurationApi.Versions}/{version}", crew.Author, ConfigurationApi.Update(new { notificationEventFamilies = new[] { securityAlert, mandatoryWithoutChannel } }), eTag))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            using HttpResponseMessage validate = await client.PostAsync($"{ConfigurationApi.Versions}/{version}/validate", crew.Reviewer);
            Assert.Equal(["content.notificationEventFamilies[0] INCOMPLETE"], await validate.ReadFieldErrorsAsync());
            eTag = AdministrationApi.ETagOf(put);
        }

        await client.WriteAndPublishAsync(crew, version, eTag, new { notificationEventFamilies = new[] { securityAlert } });

        using IServiceScope scope = host.Api.Services.CreateScope();
        ResolvedConfiguration routing = await scope.ServiceProvider.GetRequiredService<IConfigurationResolver>()
            .ResolveAsync("NOTIFICATION_ROUTING", host.Clock.GetUtcNow(), CancellationToken.None);
        var family = routing.RequireNotificationEventFamily("SECURITY_ALERT");
        Assert.True(family.IsMandatory);
        Assert.Contains(family.Channels, c => c is { Channel: Domain.Common.NotificationChannel.Sms, EnabledByDefault: true });
    }

    /// <summary>A Mandatory event family reaching R01 on the channels given.</summary>
    private static object EventFamily(string code, params (string Channel, bool EnabledByDefault)[] channels) => new
    {
        code,
        label = new { ar = "تنبيه", en = code },
        isMandatory = true,
        channels = channels.Select(c => new { channel = c.Channel, enabledByDefault = c.EnabledByDefault }).ToArray(),
        recipientRoleIds = new[] { "00000000-0000-4000-8000-000000000001" },
    };

    /// <summary>The version's row and its values, every column, as JSON text.</summary>
    private static string SnapshotOf(Guid versionId) => $"""
        SELECT row_to_json(v)::text || (SELECT coalesce(json_agg(row_to_json(c) ORDER BY c.value_key), '[]'::json)::text
                                        FROM master_data_config.configuration_value c WHERE c.configuration_version_id = v.id)
        FROM master_data_config.configuration_version v WHERE v.id = '{versionId}'
        """;

    private static async Task AssertRefusedAsync(string code, Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        Assert.Equal((HttpStatusCode.UnprocessableEntity, code), (response.StatusCode, await response.CodeOfAsync()));
    }

    private static async Task<JsonObject> ResolveAsync(HttpClient client, string token, string familyCode, DateTimeOffset? asOf)
    {
        using HttpResponseMessage response = await client.GetAsync(ConfigurationApi.Resolution(familyCode, asOf), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadObjectAsync();
    }

    private static async Task<string> EffectivityAsync(HttpClient client, string token, Guid versionId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{ConfigurationApi.Versions}/{versionId}", token);
        return (await response.ReadObjectAsync())["effectivity"]!.GetValue<string>();
    }
}
