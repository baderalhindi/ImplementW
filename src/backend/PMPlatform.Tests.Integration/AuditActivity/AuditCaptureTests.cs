using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-073's durable capture (FG-06 §4.1–4.3; M-6): an audit event is an <c>AUDIT_EVENT</c> outbox message committed with the
/// change it reports, or not at all; it is stored once, at commit, and a message that cannot be stored stays in the outbox and is
/// tried again. The API's ingestion worker idles for an hour here, so what is stored was stored by the commit path.
/// </summary>
[Collection(AuditActivitySuite.Name)]
public sealed class AuditCaptureTests(AuditActivityTestHost host)
{
    [Fact]
    public async Task AnEventCommitsWithItsChangeAndIsStoredAndProjectedAtOnce()
    {
        Guid subject = Guid.NewGuid();

        await host.StageAsync(AuditActivityTestHost.Business("Risk.RiskRegistered", AuditActivityTestHost.ProjectA, subject, 2, AuditEventClass.DataChange));

        Guid eventId = await host.EventIdAsync(subject);
        Assert.Equal(
            ["AUDIT_EVENT|Risk|true|1|"],
            await host.Database.QueryAsync($"SELECT concat_ws('|', message_type, source_module, (dispatched_at IS NOT NULL)::text, attempt_count, coalesce(last_error, '')) FROM common.outbox_message WHERE id = '{eventId}'"));
        Assert.Equal(
            ["true|RISK|Risk registered"],
            await host.Database.QueryAsync($"""
                SELECT concat_ws('|', (e.attribute_digest IS NOT NULL)::text, a.activity_kind, a.summary_en)
                FROM audit_activity.audit_event e JOIN audit_activity.business_activity_entry a ON a.audit_event_id = e.id WHERE e.id = '{eventId}'
                """));
    }

    [Fact]
    public async Task AnEventOfAUnitOfWorkThatRollsBackIsNeverCapturedNorStored()
    {
        Guid subject = Guid.NewGuid();
        await using (AsyncServiceScope scope = host.Api.Services.CreateAsyncScope())
        {
            Infrastructure.Persistence.PMPlatformDbContext context = AuditActivityTestHost.Context(scope.ServiceProvider);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            scope.ServiceProvider.GetRequiredService<IAuditTrail>().Stage(AuditActivityTestHost.Business("Risk.RiskClosed", AuditActivityTestHost.ProjectA, subject, 2));
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.Equal(["0|0"], await host.Database.QueryAsync($"""
            SELECT (SELECT count(*) FROM common.outbox_message WHERE payload::text LIKE '%{subject}%') || '|'
                || (SELECT count(*) FROM audit_activity.audit_event WHERE subject_id = '{subject}')
            """));
    }

    /// <summary>FAILED_INGESTION (AUD-CC-28): the message keeps its evidence and its error, waits its back-off, and is stored once corrected.</summary>
    [Fact]
    public async Task AMessageThatCannotBeStoredStaysInTheOutboxWithItsErrorUntilItCan()
    {
        AuditCapture capture = Capture(Guid.NewGuid()) with { SchemaVersion = 9 };
        await InsertMessageAsync(capture);
        IAuditIngestor ingestor = host.Api.Services.GetRequiredService<IAuditIngestor>();

        int stored = await ingestor.IngestAsync([capture.EventId], CancellationToken.None);
        IReadOnlyList<string> failed = await MessageAsync(capture.EventId);
        using HttpClient operations = await host.ClientAsync(1);
        System.Text.Json.Nodes.JsonObject status = await (await operations.GetAsync(AuditActivityApi.Status)).OkAsync();

        await host.Database.ExecuteAsync($"""
            UPDATE common.outbox_message SET payload = '{(capture with { SchemaVersion = AuditCapture.CurrentSchemaVersion }).Serialize()}'::jsonb, next_attempt_at = now() - interval '1 second'
            WHERE id = '{capture.EventId}'
            """);
        int retried = await ingestor.IngestDueAsync(100, CancellationToken.None);

        Assert.Equal(0, stored);
        Assert.Equal(["false|1|AUDIT_SCHEMA_UNSUPPORTED|true|0"], failed);
        Assert.True(status["failingMessages"]!.GetValue<int>() >= 1);
        Assert.Equal("AUDIT_SCHEMA_UNSUPPORTED", status["lastError"]!.GetValue<string>());
        Assert.True(retried >= 1);
        Assert.Equal(["true|2||false|1"], await MessageAsync(capture.EventId));
    }

    /// <summary>AUD-CC-07: a message delivered again finds its event already stored, and stores nothing.</summary>
    [Fact]
    public async Task ARepeatedDeliveryStoresNothingTwice()
    {
        Guid subject = Guid.NewGuid();
        await host.StageAsync(AuditActivityTestHost.Business("Risk.RiskAccepted", AuditActivityTestHost.ProjectA, subject, 2));
        Guid eventId = await host.EventIdAsync(subject);
        await host.Database.ExecuteAsync($"UPDATE common.outbox_message SET dispatched_at = NULL WHERE id = '{eventId}'");

        int stored = await host.Api.Services.GetRequiredService<IAuditIngestor>().IngestAsync([eventId], CancellationToken.None);

        Assert.Equal(0, stored);
        Assert.Equal(["true|2||false|1"], await MessageAsync(eventId));
    }

    [Fact]
    public async Task TwoIngestionsAtOnceStoreTheEventOnce()
    {
        AuditCapture capture = Capture(Guid.NewGuid());
        await InsertMessageAsync(capture);
        IAuditIngestor ingestor = host.Api.Services.GetRequiredService<IAuditIngestor>();

        int[] stored = await Task.WhenAll(ingestor.IngestAsync([capture.EventId], CancellationToken.None), ingestor.IngestAsync([capture.EventId], CancellationToken.None));

        Assert.Equal(1, stored.Sum());
        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM audit_activity.audit_event WHERE id = '{capture.EventId}'"));
    }

    /// <summary>
    /// ADR-013's participation amendment: the entity Project Manager's action is audited exactly as an internal user's — the same event,
    /// actor type and attribution, through the same trail. (A DRAFT or SUBMITTED project's events carry no project scope, by Project's
    /// own rule, so this one is not on the project's Activity timeline: record F-6.)
    /// </summary>
    [Fact]
    public async Task TheEntityProjectManagerIsAuditedAsAnInternalUserIs()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient entityManager = await host.ClientAsync(8, correlationId);

        using HttpResponseMessage withdrawn = await entityManager.PostSensitiveAsync($"/api/v1/projects/{AuditActivityTestHost.ProjectA}/withdraw");

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        string person = IdentityDatabase.UserId(8);
        Assert.Equal(
            [$"LIFECYCLE_TRANSITION|Project.SubmissionWithdrawn|SUCCESS|USER|{person}|{person}|Project|{AuditActivityTestHost.ProjectA}|true"],
            await host.Database.QueryAsync($"""
                SELECT concat_ws('|', e.event_class, e.event_type, e.outcome, e.actor_type, e.actor_user_id, e.created_by, e.subject_type, e.subject_id,
                                 (e.attribute_digest IS NOT NULL)::text)
                FROM audit_activity.audit_event e
                WHERE e.correlation_id = '{correlationId}' AND e.event_type = 'Project.SubmissionWithdrawn'
                """));
    }

    private static AuditCapture Capture(Guid subjectId) =>
        new(Guid.CreateVersion7(), AuditCapture.CurrentSchemaVersion, DateTimeOffset.UtcNow, AuditEventClass.DataChange, "Risk.RiskChanged", AuditOutcome.Success,
            AuditActorType.User, AuditActivityTestHost.Person(2), new AuditSubject("Risk", "TestRecord", subjectId), AuditActivityTestHost.ProjectA, null, null,
            Guid.NewGuid(), [AuditAttribute.Of("status", "CHANGED")]);

    /// <summary>A committed message that did not come through the trail, as a message left by a crash or another instance would be.</summary>
    private Task InsertMessageAsync(AuditCapture capture) => host.Database.ExecuteAsync($"""
        INSERT INTO common.outbox_message (id, source_module, message_type, message_key, payload, occurred_at, created_at, created_by, updated_at, updated_by)
        VALUES ('{capture.EventId}', 'Risk', 'AUDIT_EVENT', '{capture.MessageKey}', '{capture.Serialize()}'::jsonb, now(), now(),
                '{IdentityDatabase.UserId(2)}', now(), '{IdentityDatabase.UserId(2)}')
        """);

    /// <summary>dispatched|attempts|last error|waiting for a retry|events stored with this id.</summary>
    private Task<IReadOnlyList<string>> MessageAsync(Guid messageId) => host.Database.QueryAsync($"""
        SELECT concat_ws('|', (m.dispatched_at IS NOT NULL)::text, m.attempt_count, coalesce(m.last_error, ''), coalesce((m.next_attempt_at > now())::text, 'false'),
                         (SELECT count(*) FROM audit_activity.audit_event e WHERE e.id = m.id))
        FROM common.outbox_message m WHERE m.id = '{messageId}'
        """);
}
