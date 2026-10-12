using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-073 acceptance criterion 3: audit access is authorised by category, scope and sensitivity, separately from general R01
/// access (FG-06 §6; AUD-CC-02 to -05, -18, -20). Each test stages its own events and finds them by subject or correlation id.
/// </summary>
[Collection(AuditActivitySuite.Name)]
public sealed class AuditAccessTests(AuditActivityTestHost host)
{
    /// <summary>BR-AUD-023, §6.3: R01 reads administration audit, and no security, business or other category, by list or by id.</summary>
    [Fact]
    public async Task AnAdministratorReadsAdministrationAuditAndNothingElse()
    {
        (Guid administration, Guid business, Guid security) = await OneOfEachAsync();
        using HttpClient administrator = await host.ClientAsync(1);

        JsonObject page = await (await administrator.GetAsync($"{AuditActivityApi.Events}?pageSize=200")).OkAsync();
        using HttpResponseMessage securityList = await administrator.GetAsync($"{AuditActivityApi.Events}?category=SECURITY");
        using HttpResponseMessage businessList = await administrator.GetAsync($"{AuditActivityApi.Events}?category=BUSINESS");
        using HttpResponseMessage administrationEvent = await administrator.GetAsync($"{AuditActivityApi.Events}/{administration}");
        using HttpResponseMessage businessEvent = await administrator.GetAsync($"{AuditActivityApi.Events}/{business}");
        using HttpResponseMessage securityEvent = await administrator.GetAsync($"{AuditActivityApi.Events}/{security}");
        JsonObject access = await (await administrator.GetAsync(AuditActivityApi.Access)).OkAsync();

        Assert.All(page["items"]!.AsArray(), item => Assert.Equal("ADMINISTRATION", item!["category"]!.GetValue<string>()));
        Assert.Contains(administration, page.Ids());
        Assert.Equal([HttpStatusCode.Forbidden, HttpStatusCode.Forbidden], [securityList.StatusCode, businessList.StatusCode]);
        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NotFound],
            [administrationEvent.StatusCode, businessEvent.StatusCode, securityEvent.StatusCode]);
        Assert.Equal("""{"categories":["ADMINISTRATION"],"requestContextVisible":false,"activityVisible":false,"operationsPermitted":true}""", access.ToJsonString());

        // The refused category and the refused event are themselves audited (CTL-25).
        Assert.Contains("AUDIT_SECURITY_VIEW", await DeniedPermissionsAsync(1));
        Assert.Contains("AUDIT_BUSINESS_VIEW", await DeniedPermissionsAsync(1));
    }

    /// <summary>AUD-CC-04: a department reviewer sees business audit of their department's projects only, whatever they ask for.</summary>
    [Fact]
    public async Task AReviewerSeesTheProjectsTheirScopeReachesAndNoOther()
    {
        Guid inScope = Guid.NewGuid(), outOfScope = Guid.NewGuid();
        await host.StageAsync(
            AuditActivityTestHost.Business("Risk.RiskAssessed", AuditActivityTestHost.ProjectA, inScope, 2),
            AuditActivityTestHost.Business("Risk.RiskAssessed", AuditActivityTestHost.ProjectB, outOfScope, 2));
        using HttpClient reviewer = await host.ClientAsync(3);

        JsonObject projectA = await (await reviewer.GetAsync($"{AuditActivityApi.Events}?category=BUSINESS&subjectId={inScope}")).OkAsync();
        JsonObject projectB = await (await reviewer.GetAsync($"{AuditActivityApi.Events}?category=BUSINESS&projectId={AuditActivityTestHost.ProjectB}")).OkAsync();
        using HttpResponseMessage detailB = await reviewer.GetAsync($"{AuditActivityApi.Events}/{await host.EventIdAsync(outOfScope)}");

        Assert.Equal([await host.EventIdAsync(inScope)], projectA.Ids());
        Assert.Equal(0, projectB["totalCount"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.NotFound, detailB.StatusCode);
    }

    /// <summary>AUD-CC-05: an event classified above the reviewer's clearance is hidden from a reviewer whose scope reaches it.</summary>
    [Fact]
    public async Task AnEventAboveTheReviewersClearanceIsHidden()
    {
        Guid high = Guid.NewGuid(), low = Guid.NewGuid(), unclassified = Guid.NewGuid();
        await host.StageAsync(
            AuditActivityTestHost.Business("Closure.CaseSubmitted", AuditActivityTestHost.ProjectB, high, 2, classification: AuditActivityTestHost.HighClassification),
            AuditActivityTestHost.Business("Closure.CaseSubmitted", AuditActivityTestHost.ProjectB, low, 2, classification: AuditActivityTestHost.LowClassification),
            AuditActivityTestHost.Business("Closure.CaseSubmitted", AuditActivityTestHost.ProjectB, unclassified, 2));
        using HttpClient reviewer = await host.ClientAsync(2);

        JsonObject page = await (await reviewer.GetAsync($"{AuditActivityApi.Events}?eventType=Closure.CaseSubmitted&projectId={AuditActivityTestHost.ProjectB}&pageSize=200")).OkAsync();
        using HttpResponseMessage highDetail = await reviewer.GetAsync($"{AuditActivityApi.Events}/{await host.EventIdAsync(high)}");
        using HttpResponseMessage lowDetail = await reviewer.GetAsync($"{AuditActivityApi.Events}/{await host.EventIdAsync(low)}");

        Assert.Contains(await host.EventIdAsync(low), page.Ids());
        Assert.Contains(await host.EventIdAsync(unclassified), page.Ids());
        Assert.DoesNotContain(await host.EventIdAsync(high), page.Ids());
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.OK), (highDetail.StatusCode, lowDetail.StatusCode));

        // FG-06 §6.4: opening classified evidence is evidence too.
        Assert.Equal(["true"], await host.Database.QueryAsync($"""
            SELECT (count(*) = 1)::text FROM audit_activity.audit_event
            WHERE event_type = 'AuditActivity.SensitiveDetailViewed' AND actor_user_id = '{IdentityDatabase.UserId(2)}' AND subject_id = '{await host.EventIdAsync(low)}'
            """));
    }

    /// <summary>AUD.CONTEXT.SENSITIVE: request context — the client address, the name typed at sign-in — is shown only with its own permission.</summary>
    [Fact]
    public async Task RequestContextIsShownOnlyToWhoMayReadIt()
    {
        Guid correlationId = Guid.NewGuid();
        using (HttpClient anonymous = host.Api.CreateClient().WithCorrelationId(correlationId))
        {
            (await anonymous.SignInAsync("local.r06", "not the password")).Dispose();
        }

        Guid signIn = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM audit_activity.audit_event WHERE correlation_id = '{correlationId}' AND event_type = 'IdentityAccess.SignInFailed'")));
        using HttpClient withContext = await host.ClientAsync(2);
        using HttpClient withoutContext = await host.ClientAsync(3);

        JsonObject full = await (await withContext.GetAsync($"{AuditActivityApi.Events}/{signIn}")).OkAsync();
        JsonObject masked = await (await withoutContext.GetAsync($"{AuditActivityApi.Events}/{signIn}")).OkAsync();

        // The test server has no client address to record (TestServer sets no remote IP), so the name typed is the context here.
        Assert.Equal(["authentication_method", "failure_reason", "username"], Names(full));
        Assert.Empty(full["maskedAttributes"]!.AsArray());
        Assert.Equal(["authentication_method", "failure_reason"], Names(masked));
        Assert.Equal(["username"], masked["maskedAttributes"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal("SECURITY", full["event"]!["category"]!.GetValue<string>());
    }

    [Fact]
    public async Task SomeoneWithoutAnAuditPermissionSeesNoEventAndNoActivity()
    {
        (Guid administration, Guid business, _) = await OneOfEachAsync();
        using HttpClient viewer = await host.ClientAsync(6);

        JsonObject page = await (await viewer.GetAsync(AuditActivityApi.Events)).OkAsync();
        using HttpResponseMessage administrationEvent = await viewer.GetAsync($"{AuditActivityApi.Events}/{administration}");
        using HttpResponseMessage businessEvent = await viewer.GetAsync($"{AuditActivityApi.Events}/{business}");
        using HttpResponseMessage activity = await viewer.GetAsync(AuditActivityApi.Activity);
        using HttpResponseMessage operations = await viewer.GetAsync(AuditActivityApi.Status);

        Assert.Equal(0, page["totalCount"]!.GetValue<int>());
        Assert.Equal(
            [HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden],
            [administrationEvent.StatusCode, businessEvent.StatusCode, activity.StatusCode, operations.StatusCode]);
    }

    /// <summary>
    /// ADR-013's participation amendment and AUD-CC-18: an entity sees its own project's activity history — its own people named,
    /// AHDA's internal actors not — and nothing of another project, and no formal audit at all.
    /// </summary>
    [Fact]
    public async Task AnEntitySeesItsOwnActivityHistoryAndNothingElse()
    {
        Guid byEntity = Guid.NewGuid(), byAhda = Guid.NewGuid(), elsewhere = Guid.NewGuid();
        await host.StageAsync(
            AuditActivityTestHost.Business("ManagementConcern.ConcernRaised", AuditActivityTestHost.ProjectA, byEntity, 8, AuditEventClass.DataChange, entityId: AuditActivityTestHost.EntityA),
            AuditActivityTestHost.Business("Approval.TaskReturned", AuditActivityTestHost.ProjectA, byAhda, 2, AuditEventClass.ApprovalDecision),
            AuditActivityTestHost.Business("ManagementConcern.ConcernRaised", AuditActivityTestHost.ProjectB, elsewhere, 2, AuditEventClass.DataChange));
        using HttpClient entity = await host.ClientAsync(8);
        using HttpClient department = await host.ClientAsync(3);

        JsonObject own = await (await entity.GetAsync($"{AuditActivityApi.Activity}?projectId={AuditActivityTestHost.ProjectA}&pageSize=200")).OkAsync();
        JsonObject everything = await (await entity.GetAsync($"{AuditActivityApi.Activity}?pageSize=200")).OkAsync();
        JsonObject other = await (await entity.GetAsync($"{AuditActivityApi.Activity}?projectId={AuditActivityTestHost.ProjectB}")).OkAsync();
        JsonObject audit = await (await entity.GetAsync(AuditActivityApi.Events)).OkAsync();
        using HttpResponseMessage externalAudit = await entity.GetAsync($"{AuditActivityApi.Events}?category=EXTERNAL");
        JsonObject internalView = await (await department.GetAsync($"{AuditActivityApi.Activity}?subjectId={byAhda}")).OkAsync();

        JsonObject entityLine = Line(own, await host.EventIdAsync(byEntity));
        JsonObject ahdaLine = Line(own, await host.EventIdAsync(byAhda));
        Assert.Equal((IdentityDatabase.UserId(8), "Local R08", "Issue or challenge raised"),
            (entityLine["actorUserId"]!.GetValue<string>(), entityLine["actorDisplayName"]!.GetValue<string>(), entityLine["summaryEn"]!.GetValue<string>()));
        Assert.Null(ahdaLine["actorUserId"]);
        Assert.Null(ahdaLine["actorDisplayName"]);
        Assert.DoesNotContain(everything["items"]!.AsArray(), item => item!["projectId"]!.GetValue<string>() != AuditActivityTestHost.ProjectA.ToString());
        Assert.Equal(0, other["totalCount"]!.GetValue<int>());
        Assert.Equal(0, audit["totalCount"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Forbidden, externalAudit.StatusCode);
        Assert.Equal(IdentityDatabase.UserId(2), Assert.Single(internalView["items"]!.AsArray())!["actorUserId"]!.GetValue<string>());
    }

    /// <summary>An administration event, a business event on project A and a security event (a failed sign-in through the API).</summary>
    private async Task<(Guid Administration, Guid Business, Guid Security)> OneOfEachAsync()
    {
        Guid administration = Guid.NewGuid(), business = Guid.NewGuid(), correlationId = Guid.NewGuid();
        await host.StageAsync(
            new AuditEntry(AuditEventClass.PrivilegedAction, "IdentityAccess.DepartmentUpdated", AuditOutcome.Success)
            {
                ActorUserId = AuditActivityTestHost.Person(1),
                Subject = new AuditSubject("IdentityAccess", "Department", administration),
            },
            AuditActivityTestHost.Business("Risk.RiskChanged", AuditActivityTestHost.ProjectA, business, 2, AuditEventClass.DataChange));
        using (HttpClient anonymous = host.Api.CreateClient().WithCorrelationId(correlationId))
        {
            (await anonymous.SignInAsync("local.r02", "not the password")).Dispose();
        }

        Guid security = Guid.Parse(Assert.Single(await host.Database.QueryAsync($"SELECT id::text FROM audit_activity.audit_event WHERE correlation_id = '{correlationId}'")));
        return (await host.EventIdAsync(administration), await host.EventIdAsync(business), security);
    }

    private Task<IReadOnlyList<string>> DeniedPermissionsAsync(int person) => host.Database.QueryAsync($"""
        SELECT DISTINCT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
        WHERE e.event_type = 'IdentityAccess.AccessDenied' AND e.actor_user_id = '{IdentityDatabase.UserId(person)}' AND a.attribute_name = 'permission_code'
        """);

    private static IEnumerable<string> Names(JsonObject detail) => detail["attributes"]!.AsArray().Select(a => a!["name"]!.GetValue<string>());

    private static JsonObject Line(JsonObject page, Guid auditEventId) =>
        Assert.Single(page["items"]!.AsArray(), item => item!["auditEventId"]!.GetValue<string>() == auditEventId.ToString())!.AsObject();
}
