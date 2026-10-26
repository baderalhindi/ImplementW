using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence;
using PMPlatform.Infrastructure.Persistence.AuditActivity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-073 acceptance criterion 1 and the first half of its validation check: a committed audit event cannot be edited or deleted
/// by an ordinary administrator — not through the API, not through the application's data layer, not by the application's own
/// database role in SQL. The event the administrator tries is one R01 is allowed to read.
/// </summary>
[Collection(AuditActivitySuite.Name)]
public sealed class AuditImmutabilityTests(AuditActivityTestHost host)
{
    [Fact]
    public async Task NoApiRouteEditsOrDeletesAnEventTheAdministratorCanRead()
    {
        Guid eventId = await AdministrationEventAsync();
        string before = await RowAsync(eventId);
        using HttpClient administrator = await host.ClientAsync(1);

        using HttpResponseMessage read = await administrator.GetAsync($"{AuditActivityApi.Events}/{eventId}");
        using HttpResponseMessage delete = await administrator.DeleteAsync($"{AuditActivityApi.Events}/{eventId}");
        using HttpResponseMessage put = await administrator.PutAsJsonAsync($"{AuditActivityApi.Events}/{eventId}", new { outcome = "FAILED" });
        using HttpResponseMessage patch = await administrator.PatchAsJsonAsync($"{AuditActivityApi.Events}/{eventId}", new { outcome = "FAILED" });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal([HttpStatusCode.MethodNotAllowed, HttpStatusCode.MethodNotAllowed, HttpStatusCode.MethodNotAllowed], [delete.StatusCode, put.StatusCode, patch.StatusCode]);
        Assert.Equal(before, await RowAsync(eventId));
    }

    /// <summary>Whatever code tracks an event and saves — an administrator's request included — the context refuses before the database is reached.</summary>
    [Theory]
    [InlineData("update the event")]
    [InlineData("delete the event")]
    [InlineData("update an attribute")]
    [InlineData("delete an attribute")]
    public async Task TheDataLayerRefusesToChangeCommittedEvidence(string change)
    {
        Guid eventId = await AdministrationEventAsync();
        string before = await RowAsync(eventId);

        AuditEvidenceImmutableException refused = await Assert.ThrowsAsync<AuditEvidenceImmutableException>(() => host.WithScopeAsync(async services =>
        {
            PMPlatformDbContext context = AuditActivityTestHost.Context(services);
            switch (change)
            {
                case "update the event":
                    (await context.Set<AuditEvent>().SingleAsync(e => e.Id == eventId)).Outcome = AuditOutcome.Failed;
                    break;
                case "delete the event":
                    context.Remove(await context.Set<AuditEvent>().SingleAsync(e => e.Id == eventId));
                    break;
                case "update an attribute":
                    (await context.Set<AuditEventAttribute>().FirstAsync(a => a.AuditEventId == eventId)).NewValue = "TAMPERED";
                    break;
                default:
                    context.Remove(await context.Set<AuditEventAttribute>().FirstAsync(a => a.AuditEventId == eventId));
                    break;
            }

            return await context.SaveChangesAsync();
        }));

        Assert.StartsWith(AuditEvidenceImmutableException.Code, refused.Message, StringComparison.Ordinal);
        Assert.Equal(before, await RowAsync(eventId));
    }

    /// <summary>Below the data layer, the database refuses the application's own role whatever SQL it sends (TASK-033 D-3).</summary>
    [Theory]
    [InlineData("UPDATE audit_activity.audit_event SET outcome = 'FAILED' WHERE id = '{0}'")]
    [InlineData("DELETE FROM audit_activity.audit_event WHERE id = '{0}'")]
    [InlineData("UPDATE audit_activity.audit_event_attribute SET new_value = 'TAMPERED' WHERE audit_event_id = '{0}'")]
    [InlineData("DELETE FROM audit_activity.audit_event_attribute WHERE audit_event_id = '{0}'")]
    [InlineData("TRUNCATE audit_activity.audit_event_attribute")]
    public async Task TheDatabaseRefusesTheApplicationsRoleWhateverSqlItSends(string statement)
    {
        Guid eventId = await AdministrationEventAsync();
        string before = await RowAsync(eventId);

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.WithScopeAsync(services =>
            AuditActivityTestHost.Context(services).Database.ExecuteSqlRawAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, statement, eventId))));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
        Assert.Equal(before, await RowAsync(eventId));
    }

    /// <summary>A privileged action, which R01 reads under the shipped AUDIT_ADMINISTRATION_VIEW.</summary>
    private async Task<Guid> AdministrationEventAsync()
    {
        Guid subject = Guid.NewGuid();
        await host.StageAsync(new AuditEntry(AuditEventClass.PrivilegedAction, "IdentityAccess.UserUpdated", AuditOutcome.Success)
        {
            ActorUserId = AuditActivityTestHost.Person(1),
            Subject = new AuditSubject("IdentityAccess", "User", subject),
            Attributes = [AuditAttribute.Change("job_title", "Analyst", "Lead")!],
        });
        return await host.EventIdAsync(subject);
    }

    /// <summary>The event and its attributes as stored, with its hash.</summary>
    private async Task<string> RowAsync(Guid eventId) => Assert.Single(await host.Database.QueryAsync($"""
        SELECT concat_ws('|', e.outcome, e.event_hash, e.attribute_digest,
                         (SELECT string_agg(a.attribute_name || '=' || coalesce(a.old_value, '') || '>' || coalesce(a.new_value, ''), ',' ORDER BY a.attribute_name)
                          FROM audit_activity.audit_event_attribute a WHERE a.audit_event_id = e.id))
        FROM audit_activity.audit_event e WHERE e.id = '{eventId}'
        """));
}
