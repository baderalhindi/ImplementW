using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.MasterDataConfig;

/// <summary>
/// ADM-020–029 and the KPI catalogue through the API (TASK-034): the governed lifecycle on items, the API conventions,
/// the audit trail, and access for R01 only (the delivery-team decision of record F-1).
/// </summary>
[Collection(MasterDataConfigSuite.Name)]
public sealed class MasterDataEndpointTests(MasterDataConfigTestHost host)
{
    [Fact]
    public void EveryFg04EndpointNamesItsPermission()
    {
        Dictionary<string, string?> declarations = host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.StartsWith("MasterDataConfig_", StringComparison.Ordinal) == true)
            .ToDictionary(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName, EndpointAuthorization.DeclarationOf);

        Assert.Equal(26, declarations.Count);
        Assert.All(declarations, d => Assert.Equal(
            d.Key.Contains("Configuration", StringComparison.Ordinal)
                ? d.Key.StartsWith("MasterDataConfig_List", StringComparison.Ordinal) || d.Key.StartsWith("MasterDataConfig_Get", StringComparison.Ordinal)

                    ? "permission:CONFIGURATION_VIEW"
                    : "permission:CONFIGURATION_MANAGE"
                : d.Key.StartsWith("MasterDataConfig_List", StringComparison.Ordinal) || d.Key.StartsWith("MasterDataConfig_Get", StringComparison.Ordinal)
                    ? "permission:MASTER_DATA_VIEW"
                    : "permission:MASTER_DATA_MANAGE",
            d.Value));
    }

    [Fact]
    public async Task ARoleWithoutAnFg04GrantIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(MasterDataConfigTestHost.Viewer)).AccessToken;

        HttpStatusCode[] statuses =
        [
            await StatusAsync(client.GetAsync(ConfigurationApi.Catalogues, viewer)),
            await StatusAsync(client.GetAsync(ConfigurationApi.Items, viewer)),
            await StatusAsync(client.PostAsync(ConfigurationApi.Items, viewer, new { catalogueId = Guid.NewGuid(), code = "X", label = new { ar = "س", en = "X" } })),
            await StatusAsync(client.GetAsync(ConfigurationApi.KpiDefinitions, viewer)),
            await StatusAsync(client.GetAsync(ConfigurationApi.Versions, viewer)),
            await StatusAsync(client.GetAsync(ConfigurationApi.Resolution("RISK_MATRIX"), viewer)),
        ];

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Forbidden, status));
    }

    /// <summary>
    /// An item's whole life on ADM-020–029: authored DRAFT, edited by its author only, validated and published by two
    /// others, corrected once published without moving in the tree, retired, and each change audited as CONFIGURATION_CHANGE.
    /// </summary>
    [Fact]
    public async Task AnItemIsAuthoredReviewedPublishedCorrectedAndRetired()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        Guid region = await CatalogueIdAsync(client, crew.Author, "REGION");

        using HttpResponseMessage create = await client.PostAsync(ConfigurationApi.Items, crew.Author, new { catalogueId = region, code = "RIYADH", label = new { ar = "الرياض", en = "Riyadh" }, sortOrder = 1 });
        JsonObject created = await create.ReadObjectAsync();
        Guid item = AdministrationApi.IdOf(created);
        Assert.Equal((HttpStatusCode.Created, "DRAFT"), (create.StatusCode, created["governance"]!["lifecycleState"]!.GetValue<string>()));
        Assert.Equal($"{ConfigurationApi.Items}/{item}", create.Headers.Location!.ToString());

        using (HttpResponseMessage duplicate = await client.PostAsync(ConfigurationApi.Items, crew.Author, new { catalogueId = region, code = "RIYADH", label = new { ar = "الرياض", en = "Riyadh" } }))
        {
            Assert.Equal((HttpStatusCode.Conflict, "MASTER_DATA_CONFIG_DUPLICATE_KEY"), (duplicate.StatusCode, await duplicate.CodeOfAsync()));
            Assert.Equal(["code DUPLICATE"], await duplicate.ReadFieldErrorsAsync());
        }

        object correction = new { label = new { ar = "منطقة الرياض", en = "Riyadh Region" }, sortOrder = 1 };
        using (HttpResponseMessage notTheAuthor = await client.PutAsync($"{ConfigurationApi.Items}/{item}", crew.Reviewer, correction, AdministrationApi.ETagOf(create)))
        {
            Assert.Equal("MASTER_DATA_CONFIG_SEPARATION_OF_DUTIES", await notTheAuthor.CodeOfAsync());
        }

        await StepAsync(client, crew.Reviewer, item, "validate", HttpStatusCode.OK);
        await StepAsync(client, crew.Reviewer, item, "publish", HttpStatusCode.UnprocessableEntity);
        await StepAsync(client, crew.Publisher, item, "publish", HttpStatusCode.OK);

        using HttpResponseMessage published = await client.GetAsync($"{ConfigurationApi.Items}/{item}", crew.Author);
        using (HttpResponseMessage moved = await client.PutAsync(
                   $"{ConfigurationApi.Items}/{item}", crew.Reviewer, new { label = new { ar = "الرياض", en = "Riyadh" }, parentItemId = item, sortOrder = 1 }, AdministrationApi.ETagOf(published)))
        {
            Assert.Equal("MASTER_DATA_CONFIG_PUBLISHED_IMMUTABLE", await moved.CodeOfAsync());
        }

        using (HttpResponseMessage stale = await client.PutAsync($"{ConfigurationApi.Items}/{item}", crew.Reviewer, correction, AdministrationApi.ETagOf(create)))
        {
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        }

        using (HttpResponseMessage corrected = await client.PutAsync($"{ConfigurationApi.Items}/{item}", crew.Reviewer, correction, AdministrationApi.ETagOf(published)))
        {
            JsonObject body = await corrected.ReadObjectAsync();
            Assert.Equal((HttpStatusCode.OK, "Riyadh Region", "PUBLISHED"), (corrected.StatusCode, body["label"]!["en"]!.GetValue<string>(), body["governance"]!["lifecycleState"]!.GetValue<string>()));
        }

        using IServiceScope scope = host.Api.Services.CreateScope();
        IMasterDataResolver resolver = scope.ServiceProvider.GetRequiredService<IMasterDataResolver>();
        Assert.Equal("RIYADH", (await resolver.RequirePublishedItemAsync("REGION", item, CancellationToken.None)).Code);
        await Assert.ThrowsAsync<ConfigurationMissingException>(() => resolver.RequirePublishedItemAsync("CITY", item, CancellationToken.None));

        await StepAsync(client, crew.Publisher, item, "retire", HttpStatusCode.OK);
        await StepAsync(client, crew.Publisher, item, "retire", HttpStatusCode.Conflict);
        await Assert.ThrowsAsync<ConfigurationMissingException>(() => resolver.RequirePublishedItemAsync("REGION", item, CancellationToken.None));

        Assert.Equal(
            ["ItemCreated", "ItemValidated", "ItemPublished", "ItemUpdated", "ItemRetired"],
            (await host.Database.QueryAsync($"""
                SELECT e.event_class || ' ' || e.event_type FROM audit_activity.audit_event e
                WHERE e.subject_id = '{item}' ORDER BY e.occurred_at, e.id
                """)).Select(e => e.Replace("CONFIGURATION_CHANGE MasterDataConfig.", string.Empty, StringComparison.Ordinal)));
    }

    /// <summary>A shipped item (the governance profiles of ADR-015) is never retired: domain rules name it.</summary>
    [Fact]
    public async Task AShippedItemIsNeverRetired()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        string light = (await host.Database.QueryAsync("""
            SELECT i.id::text FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'LIGHT'
            """)).Single();

        using HttpResponseMessage retire = await client.PostAsync($"{ConfigurationApi.Items}/{light}/retire", crew.Publisher);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "MASTER_DATA_CONFIG_SYSTEM_ROW"), (retire.StatusCode, await retire.CodeOfAsync()));
    }

    /// <summary>A KPI's unit is a PUBLISHED KPI_UNIT item; once the KPI is PUBLISHED its unit and direction are fixed.</summary>
    [Fact]
    public async Task AKpiDefinitionTakesAPublishedUnitAndKeepsItOncePublished()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        Guid unitCatalogue = await CatalogueIdAsync(client, crew.Author, "KPI_UNIT");
        using HttpResponseMessage unit = await client.PostAsync(ConfigurationApi.Items, crew.Author, new { catalogueId = unitCatalogue, code = "PERCENT", label = new { ar = "نسبة مئوية", en = "Percent" } });
        Guid unitId = AdministrationApi.IdOf(await unit.ReadObjectAsync());
        object kpi = new { code = "SCHEDULE_PERFORMANCE", name = new { ar = "أداء الجدول", en = "Schedule performance" }, unitItemId = unitId, direction = "HIGHER_IS_BETTER" };

        using (HttpResponseMessage draftUnit = await client.PostAsync(ConfigurationApi.KpiDefinitions, crew.Author, kpi))
        {
            Assert.Equal("MASTER_DATA_CONFIG_REFERENCE_INVALID", await draftUnit.CodeOfAsync());
            Assert.Equal(["unitItemId NOT_PUBLISHED"], await draftUnit.ReadFieldErrorsAsync());
        }

        await StepAsync(client, crew.Reviewer, unitId, "validate", HttpStatusCode.OK);
        await StepAsync(client, crew.Publisher, unitId, "publish", HttpStatusCode.OK);
        using HttpResponseMessage create = await client.PostAsync(ConfigurationApi.KpiDefinitions, crew.Author, kpi);
        Guid kpiId = AdministrationApi.IdOf(await create.ReadObjectAsync());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        foreach ((string token, string step) in new[] { (crew.Reviewer, "validate"), (crew.Publisher, "publish") })
        {
            using HttpResponseMessage response = await client.PostAsync($"{ConfigurationApi.KpiDefinitions}/{kpiId}/{step}", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using HttpResponseMessage current = await client.GetAsync($"{ConfigurationApi.KpiDefinitions}/{kpiId}", crew.Author);
        using HttpResponseMessage redirected = await client.PutAsync(
            $"{ConfigurationApi.KpiDefinitions}/{kpiId}", crew.Author,
            new { name = new { ar = "أداء الجدول", en = "Schedule performance" }, unitItemId = unitId, direction = "LOWER_IS_BETTER" }, AdministrationApi.ETagOf(current));

        Assert.Equal("MASTER_DATA_CONFIG_PUBLISHED_IMMUTABLE", await redirected.CodeOfAsync());
        Assert.Equal(["direction NOT_ALLOWED"], await redirected.ReadFieldErrorsAsync());
    }

    /// <summary>The content body: shape is 400 with every path (T-3), meaning is 422 with every path, a PUT needs If-Match.</summary>
    [Fact]
    public async Task ContentIsCheckedForShapeThenMeaningAndNeedsTheVersionsETag()
    {
        using HttpClient client = host.Api.CreateClient();
        Crew crew = await client.SignInCrewAsync();
        (Guid version, string eTag) = await client.CreateVersionAsync(crew, "APPROVAL_AUTHORITY");
        string path = $"{ConfigurationApi.Versions}/{version}";

        using (HttpResponseMessage noIfMatch = await client.PutAsync(path, crew.Author, ConfigurationApi.Update(new { }), ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, noIfMatch.StatusCode);
        }

        using (HttpResponseMessage shape = await client.PutAsync(path, crew.Author, ConfigurationApi.Update(new { approvalAuthority = new object[] { new { subjectTypeCode = "BASELINE" } } }), eTag))
        {
            Assert.Equal(HttpStatusCode.BadRequest, shape.StatusCode);
            Assert.Equal(["content.approvalAuthority[0].sequenceNo REQUIRED", "content.approvalAuthority[0].approverRoleId REQUIRED"], await shape.ReadFieldErrorsAsync());
        }

        object meaning = new
        {
            approvalAuthority = new[] { new { subjectTypeCode = "BASELINE", bandNo = 4, sequenceNo = 1, approverRoleId = Guid.NewGuid() } },
            riskRatings = new[] { new { code = "HIGH", label = new { ar = "مرتفع", en = "High" } } },
        };
        using (HttpResponseMessage invalid = await client.PutAsync(path, crew.Author, ConfigurationApi.Update(meaning), eTag))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "MASTER_DATA_CONFIG_CONTENT_INVALID"), (invalid.StatusCode, await invalid.CodeOfAsync()));
            Assert.Equal(
                ["content.riskRatings SECTION_NOT_ALLOWED", "content.approvalAuthority[0].bandNo OUT_OF_RANGE", "content.approvalAuthority[0].approverRoleId NOT_FOUND"],
                await invalid.ReadFieldErrorsAsync());
        }

        object valid = new { approvalAuthority = new[] { new { subjectTypeCode = "BASELINE", bandNo = 3, sequenceNo = 1, approverRoleId = "00000000-0000-4000-8000-000000000002" } } };
        using (HttpResponseMessage saved = await client.PutAsync(path, crew.Author, ConfigurationApi.Update(valid), eTag))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.NotEqual(eTag, AdministrationApi.ETagOf(saved));
        }

        using HttpResponseMessage lost = await client.PutAsync(path, crew.Author, ConfigurationApi.Update(valid), eTag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, lost.StatusCode);
    }

    private static async Task<Guid> CatalogueIdAsync(HttpClient client, string token, string code)
    {
        using HttpResponseMessage response = await client.GetAsync(ConfigurationApi.Catalogues, token);
        return AdministrationApi.IdOf((await response.ReadArrayAsync()).Select(c => c!.AsObject()).Single(c => c["code"]!.GetValue<string>() == code));
    }

    private static async Task StepAsync(HttpClient client, string token, Guid itemId, string step, HttpStatusCode expected)
    {
        using HttpResponseMessage response = await client.PostAsync($"{ConfigurationApi.Items}/{itemId}/{step}", token);
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        return response.StatusCode;
    }
}
