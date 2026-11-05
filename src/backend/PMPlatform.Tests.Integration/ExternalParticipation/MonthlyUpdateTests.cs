using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.ExternalParticipation.ExternalParticipationDriver;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>The monthly update (the 5 Nov 2026 review §2) and the demo reset (§5), which removes what it leaves behind.</summary>
[Collection(ExternalParticipationSuite.Name)]
public sealed class MonthlyUpdateTests(ExternalParticipationTestHost host)
{
    private const string Batches = "/api/v1/update-request-batches";
    private const string Mine = "/api/v1/my-update-requests";

    private static object[] Update(decimal progress) =>
    [
        new { fieldCode = "actualProgressPercent", value = progress.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        new { fieldCode = "nextAction", value = "Meet the contractor", language = "en" },
        new { fieldCode = "note", value = "Monthly note", language = "en" },
    ];

    private static object BatchBody(IEnumerable<(Guid Project, int? Responder)> items, DateOnly due, bool repeat = false) => new
    {
        items = items.Select(i => new { projectId = i.Project, responsibleUserId = i.Responder is { } r ? Person(r) : (Guid?)null }),
        dueDate = Iso(due),
        repeatMonthly = repeat,
        repeatDay = repeat ? 1 : (int?)null,
        instructions = "Update November",
        reviewerUserId = Person(3),
    };

    private static string[] States(JsonObject batch) => [.. batch["rows"]!.AsArray().Select(r => r!["state"]!.GetValue<string>())];

    [Fact]
    public async Task ABatchReachesItsResponderWhoseAcceptedAnswerWritesToTheProject()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid first = await host.ProjectAsync();
        Guid second = await host.ProjectAsync();
        Guid other = await host.ProjectAsync(ExternalParticipationTestHost.EntityB);
        await host.GrantProjectAsync(8, first);
        await host.GrantProjectAsync(8, second);
        await host.GrantProjectAsync(7, other);

        // A project with no contact and no responder given names itself; nothing is created.
        HttpResponseMessage refused = await client.SendAsync(HttpMethod.Post, Batches, sessions.DepartmentManager, BatchBody([(first, null)], Today.AddDays(5)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        JsonObject batch = await client.CreatedOrFailAsync(sessions.DepartmentManager, Batches, BatchBody([(first, 8), (second, 8), (other, 7)], Today.AddDays(5)));
        Assert.Equal(3, batch["batch"]!["projectCount"]!.GetValue<int>());
        Assert.Equal(2, batch["batch"]!["entityCount"]!.GetValue<int>());
        Assert.All(States(batch), s => Assert.Equal("SENT", s));
        Guid batchId = Guid.Parse(batch["batch"]!.Text("id"));

        JsonObject queue = await client.GetOrFailAsync(sessions.EntityA, Mine);
        Assert.True(queue["totalCount"]!.GetValue<int>() >= 2);
        JsonNode request = queue["items"]!.AsArray().First(i => Guid.Parse(i!.Text("projectId")) == first)!;
        Guid requestId = Guid.Parse(request.Text("requestId"));

        // A draft survives a closed window, and the batch page follows it.
        Guid contribution = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityA, Contributions, new { externalUpdateRequestId = requestId, fields = Update(45m) }));
        JsonNode reopened = (await client.GetOrFailAsync(sessions.EntityA, Mine))["items"]!.AsArray().First(i => Guid.Parse(i!.Text("requestId")) == requestId)!;
        Assert.Equal("45", reopened["prefill"]!["actualProgressPercent"]!.GetValue<string>());

        await client.OkOrFailAsync(sessions.EntityA, $"{Contributions}/{contribution}/submit");
        Assert.DoesNotContain((await client.GetOrFailAsync(sessions.EntityA, Mine))["items"]!.AsArray(), i => Guid.Parse(i!.Text("requestId")) == requestId);

        await client.DecideAsync(sessions.DepartmentManager, contribution, "accept");
        JsonObject project = await client.GetOrFailAsync(sessions.ProjectManager, $"/api/v1/projects/{first}");
        Assert.Equal(45m, project["followUp"]!["declaredActualPercent"]!.GetValue<decimal>());
        Assert.Equal("ENTITY_UPDATE", project["followUp"]!["declaredSource"]!.GetValue<string>());
        Assert.Contains("ACCEPTED", States(await client.GetOrFailAsync(sessions.DepartmentManager, $"{Batches}/{batchId}")));

        // Withdraw: the open ones are cancelled, the accepted one stands.
        JsonObject withdrawn = await client.OkOrFailAsync(sessions.DepartmentManager, $"{Batches}/{batchId}/withdraw", new { reason = "No longer needed" });
        Assert.Equal(["ACCEPTED", "WITHDRAWN", "WITHDRAWN"], States(withdrawn).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnInternalProjectManagerAnswersAMonthlyUpdateButOtherSchemasStayExternal()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid project = await host.ProjectAsync();

        JsonObject batch = await client.CreatedOrFailAsync(sessions.DepartmentManager, Batches, BatchBody([(project, 5)], Today.AddDays(5)));
        Guid request = Guid.Parse(batch["rows"]!.AsArray()[0]!.Text("requestId"));
        Assert.Contains((await client.GetOrFailAsync(sessions.ProjectManager, Mine))["items"]!.AsArray(), i => Guid.Parse(i!.Text("requestId")) == request);
        await client.CreatedOrFailAsync(sessions.ProjectManager, Contributions, new { externalUpdateRequestId = request, fields = Update(10m) });

        await Assert.ThrowsAnyAsync<Exception>(() => host.Database.ExecuteAsync($"""
            INSERT INTO external_participation.external_update_request (id, project_id, external_entity_id, origin, contribution_type_item_id, contribution_schema_code, instructions, instructions_lang,
                responsible_user_id, status, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{project}', '{ExternalParticipationTestHost.EntityA}', 'AHDA_ISSUED', '{ExternalParticipationTestHost.InformationTypeId}', 'PROJECT_INFORMATION', 'x', 'en',
                '{Person(5)}', 'DRAFT', now(), '{Person(5)}', now(), '{Person(5)}')
            """));
    }

    [Fact]
    public async Task ADepartmentManagerSendsOnlyInsideTheirScope()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid project = await host.ProjectAsync();
        await host.GrantProjectAsync(8, project);

        HttpResponseMessage noGrant = await client.SendAsync(HttpMethod.Post, Batches, sessions.ProjectManager, BatchBody([(project, 8)], Today.AddDays(5)));
        Assert.Equal(HttpStatusCode.Forbidden, noGrant.StatusCode);
    }

    [Fact]
    public async Task TheScheduledPassRemindsOnceADayAndSendsTheNextBatchOfASeries()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid project = await host.ProjectAsync();
        await host.GrantProjectAsync(8, project);
        JsonObject batch = await client.CreatedOrFailAsync(sessions.DepartmentManager, Batches, BatchBody([(project, 8)], Today.AddDays(3), repeat: true));
        Guid batchId = Guid.Parse(batch["batch"]!.Text("id"));
        Guid request = Guid.Parse(batch["rows"]!.AsArray()[0]!.Text("requestId"));

        await RunPassAsync();
        Assert.Equal(1, await RemindersAsync(request));
        await RunPassAsync();
        Assert.Equal(1, await RemindersAsync(request));

        await host.Database.ExecuteAsync($"UPDATE external_participation.update_request_batch SET next_run_on = '{Iso(Today)}' WHERE id = '{batchId}'");
        await RunPassAsync();
        long sent = await CountAsync($"SELECT count(*)::text FROM external_participation.update_request_batch WHERE series_id = '{batchId}'");
        Assert.Equal(2, sent);
        Assert.Equal(1, await CountAsync($"SELECT count(*)::text FROM external_participation.update_request_batch WHERE id = '{batchId}' AND next_run_on IS NULL"));
    }

    /// <summary>The 5 Nov 2026 review §5: a project marked DEMO goes with what hangs on it; another stays; the assignment keeps the person's role; the trail stays and gains one entry.</summary>
    [Fact]
    public async Task TheResetRemovesMarkedProjectsAndWhatHangsOnThemOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        ParticipationSessions sessions = await client.SignInAsync();
        Guid demo = await host.ProjectAsync();
        Guid real = await host.ProjectAsync();
        await host.GrantProjectAsync(8, demo);
        JsonObject batch = await client.CreatedOrFailAsync(sessions.DepartmentManager, Batches, BatchBody([(demo, 8)], Today.AddDays(5)));
        Guid request = Guid.Parse(batch["rows"]!.AsArray()[0]!.Text("requestId"));
        await client.CreatedOrFailAsync(sessions.EntityA, Contributions, new { externalUpdateRequestId = request, fields = Update(20m) });
        await host.Database.ExecuteAsync($"UPDATE project.project SET seed_source = 'DEMO' WHERE id = '{demo}'");
        long auditBefore = await CountAsync("SELECT count(*)::text FROM audit_activity.audit_event");

        Assert.Equal(0, await DemoDataReset.RunAsync(host.Api.Services, [DemoDataReset.Command, DemoDataReset.DryRunOption]));
        Assert.Equal(1, await CountAsync($"SELECT count(*)::text FROM project.project WHERE id = '{demo}'"));

        Assert.Equal(0, await DemoDataReset.RunAsync(host.Api.Services, [DemoDataReset.Command]));
        Assert.Equal(0, await CountAsync($"SELECT count(*)::text FROM project.project WHERE id = '{demo}'"));
        Assert.Equal(1, await CountAsync($"SELECT count(*)::text FROM project.project WHERE id = '{real}'"));
        Assert.Equal(0, await CountAsync($"SELECT count(*)::text FROM external_participation.external_update_request WHERE id = '{request}'"));
        Assert.Equal(0, await CountAsync($"SELECT count(*)::text FROM external_participation.external_contribution WHERE external_update_request_id = '{request}'"));
        Assert.True(await CountAsync("SELECT count(*)::text FROM audit_activity.audit_event") > auditBefore);
        Assert.Equal(1, await CountAsync($"SELECT count(*)::text FROM audit_activity.audit_event WHERE event_type = '{DemoDataReset.RemovedEvent}'"));
    }

    private async Task<long> CountAsync(string sql) => long.Parse((await host.Database.QueryAsync(sql)).Single(), System.Globalization.CultureInfo.InvariantCulture);

    private async Task RunPassAsync()
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMonthlyUpdateMaintenance>().RunAsync(500, CancellationToken.None);
    }

    private Task<long> RemindersAsync(Guid request) =>
        CountAsync($"SELECT count(*)::text FROM common.outbox_message WHERE message_type = 'NOTIFICATION_INTENT' AND message_key LIKE '%{request}%' AND payload::text LIKE '%DUE_SOON%'");
}
