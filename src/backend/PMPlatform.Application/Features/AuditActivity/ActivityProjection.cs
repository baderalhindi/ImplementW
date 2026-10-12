using System.Text.Json;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// The one rule that turns an audit event into an Activity line (FG-06 §7.1), applied by the live projection and by every rebuild
/// alike, so the projection is a function of the audit store and nothing else. An event becomes activity when it is a successful
/// lifecycle transition, data change or approval decision of a project, and its type has a template; anything else is left out.
/// </summary>
public static class ActivityProjection
{
    private static readonly AuditEventClass[] ProjectedClasses = [AuditEventClass.LifecycleTransition, AuditEventClass.DataChange, AuditEventClass.ApprovalDecision];

    /// <summary>The Activity line of <paramref name="auditEvent"/>, or null when it is not activity.</summary>
    public static BusinessActivityEntry? Project(AuditEvent auditEvent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return auditEvent.ScopeProjectId is null
               || auditEvent.Outcome != AuditOutcome.Success
               || !ProjectedClasses.Contains(auditEvent.EventClass)
               || !ActivityTemplateCatalogue.Templates.TryGetValue(auditEvent.EventType, out ActivityTemplate? template)
            ? null
            : new BusinessActivityEntry
            {
                Id = Guid.CreateVersion7(now),
                AuditEventId = auditEvent.Id,
                ProjectId = auditEvent.ScopeProjectId,
                OccurredAt = auditEvent.OccurredAt,
                ActorUserId = auditEvent.ActorUserId,
                ActivityKind = KindOf(auditEvent.EventType),
                SummaryAr = template.SummaryAr,
                SummaryEn = template.SummaryEn,
                CreatedAt = now,
                CreatedBy = AuditCapturePrincipal.Id,
                UpdatedAt = now,
                UpdatedBy = AuditCapturePrincipal.Id,
            };
    }

    /// <summary>The source domain of an event type, in upper snake case: <c>ManagementConcern.ConcernRaised</c> is <c>MANAGEMENT_CONCERN</c>.</summary>
    public static string KindOf(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return JsonNamingPolicy.SnakeCaseUpper.ConvertName(eventType[..eventType.IndexOf('.', StringComparison.Ordinal)]);
    }

    /// <summary>Whether a stored line says what the rule derives: the same project, time, actor, domain and wording. Row bookkeeping is not compared.</summary>
    public static bool SameContent(BusinessActivityEntry expected, BusinessActivityEntry stored)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(stored);
        return expected.AuditEventId == stored.AuditEventId
               && expected.ProjectId == stored.ProjectId
               && expected.OccurredAt == stored.OccurredAt
               && expected.ActorUserId == stored.ActorUserId
               && string.Equals(expected.ActivityKind, stored.ActivityKind, StringComparison.Ordinal)
               && string.Equals(expected.SummaryAr, stored.SummaryAr, StringComparison.Ordinal)
               && string.Equals(expected.SummaryEn, stored.SummaryEn, StringComparison.Ordinal);
    }

    /// <summary>Gives a stored line the content the rule derives, keeping its row identity.</summary>
    public static void CopyContent(BusinessActivityEntry from, BusinessActivityEntry to, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        to.ProjectId = from.ProjectId;
        to.OccurredAt = from.OccurredAt;
        to.ActorUserId = from.ActorUserId;
        to.ActivityKind = from.ActivityKind;
        to.SummaryAr = from.SummaryAr;
        to.SummaryEn = from.SummaryEn;
        to.UpdatedAt = now;
        to.UpdatedBy = AuditCapturePrincipal.Id;
    }
}
