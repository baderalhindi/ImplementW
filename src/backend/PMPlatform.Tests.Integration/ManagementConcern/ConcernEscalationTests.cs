using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Notifications;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.ManagementConcern;

/// <summary>
/// TASK-057's second acceptance criterion: an escalated concern generates a WF-15 NotificationIntent exactly once per escalation
/// event — no duplicate on retry — and the escalation is an overlay routed to its role, which never moves the concern.
/// </summary>
[Collection(ConcernSuite.Name)]
public sealed class ConcernEscalationTests(ConcernTestHost host)
{
    /// <summary>
    /// The workbook's check, as an idempotency test: the escalation is triggered twice by retry — the same request with the same
    /// <c>Idempotency-Key</c> — and the outbox message is delivered again, by the dispatcher and straight to WF-15's consumer. One
    /// escalation, one outbox message, one NotificationIntent, one notification to the Department Manager it is routed to.
    /// </summary>
    [Fact]
    public async Task ARetriedEscalationGeneratesExactlyOneNotification()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync(), ConcernDriver.Impacts(await host.DimensionsAsync(), 3));
        Guid key = Guid.NewGuid();

        string escalationId;
        using (HttpResponseMessage first = await client.EscalateAsync(sessions.InternalManager, concernId, key))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.False(first.Headers.Contains("Idempotent-Replayed"));
            escalationId = (await first.ReadObjectAsync()).Text("id");
        }

        await host.Api.DeliverAsync();

        // The retry: the same key and body find the escalation raised, answer as the first did, and publish nothing.
        using (HttpResponseMessage retry = await client.EscalateAsync(sessions.InternalManager, concernId, key))
        {
            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.Equal(["true"], retry.Headers.GetValues("Idempotent-Replayed"));
            Assert.Equal($"/api/v1/concern-escalations/{escalationId}", retry.Headers.Location!.OriginalString);
            Assert.Equal(escalationId, (await retry.ReadObjectAsync()).Text("id"));
        }

        await host.Api.DeliverAsync();

        // The message delivered again: the dispatcher finds it dispatched, and WF-15's consumer, handed the same payload, records nothing.
        Guid messageId = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = '{ConcernDriver.EscalatedEvent}:{escalationId}'")));
        Assert.False(await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(messageId, CancellationToken.None));
        string payload = Assert.Single(await host.Database.QueryAsync($"SELECT payload::text FROM common.outbox_message WHERE id = '{messageId}'"));
        await using (AsyncServiceScope scope = host.Api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationIntentConsumer>().HandleAsync(payload, CancellationToken.None);
        }

        await host.Api.ProcessAsync();

        Assert.Equal(["1|1|1"], await host.Database.QueryAsync($"""
            SELECT (SELECT count(*) FROM management_concern.concern_escalation WHERE management_concern_id = '{concernId}')::text || '|'
                || (SELECT count(*) FROM common.outbox_message WHERE message_key LIKE '{ConcernDriver.EscalatedEvent}:%' AND payload->'subject'->>'id' = '{concernId}')::text || '|'
                || (SELECT count(*) FROM notifications.notification_intent WHERE source_event_type = '{ConcernDriver.EscalatedEvent}' AND subject_id = '{concernId}')::text
            """));
        Assert.Equal(["3|IN_APP|SENT|"], await host.Database.QueryAsync(NotificationDriver.DeliveriesOf(ConcernDriver.EscalatedEvent, Guid.Parse(escalationId))));
        Assert.Single(await host.AuditEventsAsync(concernId), e => e == "ManagementConcern.ConcernEscalated");
    }

    /// <summary>
    /// Exactly once per escalation event, not once per concern: an escalation resolved and a second one raised are two events with two
    /// intents, each keyed by its own escalation. Replaying the first request then finds the first escalation, as it is now.
    /// </summary>
    [Fact]
    public async Task EachEscalationEventIsNotifiedOnce()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync());
        Guid firstKey = Guid.NewGuid();

        string first = await EscalateOrFailAsync(client, sessions.InternalManager, concernId, firstKey);
        await client.OkOrFailAsync(sessions.DepartmentManager, $"{ConcernDriver.Escalations}/{first}/resolve", ConcernDriver.Resolution("Utility owner reopens the road on Sunday."));
        string second = await EscalateOrFailAsync(client, sessions.InternalManager, concernId, Guid.NewGuid());
        await host.Api.DeliverAsync();

        using (HttpResponseMessage replay = await client.EscalateAsync(sessions.InternalManager, concernId, firstKey))
        {
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            JsonObject escalation = await replay.ReadObjectAsync();
            Assert.Equal((first, "RESOLVED", 1), (escalation.Text("id"), escalation.Text("status"), escalation["escalationNo"]!.GetValue<int>()));
        }

        await host.Api.DeliverAsync();
        Assert.Equal(
            [first, second],
            await host.Database.QueryAsync($"SELECT source_reference FROM notifications.notification_intent WHERE subject_id = '{concernId}' ORDER BY occurred_at"));
        JsonArray listed = (await client.GetOrFailAsync(sessions.DepartmentManager, $"{ConcernDriver.Escalations}?managementConcernId={concernId}"))["items"]!.AsArray();
        Assert.Equal([(2, "OPEN"), (1, "RESOLVED")], listed.Select(e => (e!["escalationNo"]!.GetValue<int>(), e.Text("status"))));
    }

    /// <summary>
    /// A concern holds one OPEN escalation: a new request while it is open is refused and publishes nothing; the same key sent with
    /// another body is a reuse (R-37), refused too.
    /// </summary>
    [Fact]
    public async Task ASecondRequestWhileAnEscalationIsOpenPublishesNothing()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync());
        Guid key = Guid.NewGuid();
        await EscalateOrFailAsync(client, sessions.InternalManager, concernId, key);

        using (HttpResponseMessage another = await client.EscalateAsync(sessions.InternalManager, concernId, Guid.NewGuid()))
        {
            await another.AssertStatusAsync(HttpStatusCode.Conflict, "CONCERN_ESCALATION_OPEN");
        }

        using (HttpResponseMessage reused = await client.EscalateAsync(sessions.InternalManager, concernId, key, reason: "A different reason."))
        {
            await reused.AssertStatusAsync(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED");
        }

        Assert.Equal(["1"], await host.Database.QueryAsync(
            $"SELECT count(*)::text FROM common.outbox_message WHERE message_key LIKE '{ConcernDriver.EscalatedEvent}:%' AND payload->'subject'->>'id' = '{concernId}'"));
    }

    /// <summary>
    /// An escalation is an overlay (ISS-GP-06): it goes to the role WORKFLOW_POLICY routes it to, the concern keeps its status, and only
    /// that role resolves it — a holder of the same permission through another role does not; its escalator alone withdraws one.
    /// </summary>
    [Fact]
    public async Task AnEscalationIsRoutedToItsRoleAndNeverMovesTheConcern()
    {
        using HttpClient client = host.Api.CreateClient();
        ConcernSessions sessions = await client.SignInAsync();
        Guid concernId = await client.RaiseAsync(sessions.InternalManager, await host.ProjectAsync());
        await client.CommandOrFailAsync(sessions.Officer, concernId, "assign", new { assigneeUserId = ConcernDriver.Person(3) });

        string escalationId = await EscalateOrFailAsync(client, sessions.InternalManager, concernId, Guid.NewGuid());
        JsonObject concern = await client.ConcernAsync(sessions.InternalManager, concernId);
        Assert.Equal(("ASSIGNED", escalationId), (concern.Text("status"), concern["openEscalation"]!.Text("id")));
        Assert.Equal(["R03"], await host.Database.QueryAsync($"""
            SELECT r.code FROM management_concern.concern_escalation e JOIN identity_access.role r ON r.id = e.escalated_to_role_id WHERE e.id = '{escalationId}'
            """));

        // local.r02 holds CONCERN_ESCALATION_RESOLVE at ALL, through R02; the escalation is addressed to R03.
        using (HttpResponseMessage otherRole = await client.PostAsync($"{ConcernDriver.Escalations}/{escalationId}/resolve", sessions.Officer, ConcernDriver.Resolution("Not mine.")))
        {
            await otherRole.AssertStatusAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        }

        using (HttpResponseMessage withdrawnByAnother = await client.PostAsync($"{ConcernDriver.Escalations}/{escalationId}/withdraw", sessions.Officer))
        {
            await withdrawnByAnother.AssertStatusAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        }

        JsonObject resolved = await client.OkOrFailAsync(sessions.DepartmentManager, $"{ConcernDriver.Escalations}/{escalationId}/resolve", ConcernDriver.Resolution("Road reopens Sunday."));
        Assert.Equal(("RESOLVED", ConcernDriver.Person(3).ToString()), (resolved.Text("status"), resolved.Text("resolvedByUserId")));
        using (HttpResponseMessage again = await client.PostAsync($"{ConcernDriver.Escalations}/{escalationId}/resolve", sessions.DepartmentManager, ConcernDriver.Resolution("Again.")))
        {
            await again.AssertStatusAsync(HttpStatusCode.Conflict, "CONCERN_ESCALATION_NOT_OPEN");
        }

        string second = await EscalateOrFailAsync(client, sessions.InternalManager, concernId, Guid.NewGuid());
        Assert.Equal("WITHDRAWN", (await client.OkOrFailAsync(sessions.InternalManager, $"{ConcernDriver.Escalations}/{second}/withdraw")).Text("status"));
        concern = await client.ConcernAsync(sessions.InternalManager, concernId);
        Assert.Equal("ASSIGNED", concern.Text("status"));
        Assert.Null(concern["openEscalation"]);
        Assert.Equal(
            ["ManagementConcern.ConcernRaised", "ManagementConcern.ConcernAssigned", "ManagementConcern.ConcernEscalated", "ManagementConcern.EscalationResolved",
             "ManagementConcern.ConcernEscalated", "ManagementConcern.EscalationWithdrawn"],
            await host.AuditEventsAsync(concernId));
    }

    private static async Task<string> EscalateOrFailAsync(HttpClient client, string token, Guid concernId, Guid key)
    {
        using HttpResponseMessage escalated = await client.EscalateAsync(token, concernId, key);
        Assert.True(escalated.StatusCode == HttpStatusCode.Created, $"escalate: {(int)escalated.StatusCode} {await escalated.Content.ReadAsStringAsync()}");
        return (await escalated.ReadObjectAsync()).Text("id");
    }
}
