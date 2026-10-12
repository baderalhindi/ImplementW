using System.Linq.Expressions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// The audit category of an event, and the permission that reads it (FG-06 §16; AUD-CC-03). The category follows from the event's
/// class and, for business classes, from the module that produced it — WF-14's events are FINANCIAL and WF-13's EXTERNAL — so it
/// is fixed when the event is stored. Each category's permission is in a group of its own, so holding one category says nothing
/// about an event of another: such an event is answered as if it did not exist (R-47).
/// </summary>
public static class AuditCategories
{
    private const string FinancialProducer = "FinancialKpi.";
    private const string ExternalProducer = "ExternalParticipation.";

    private static readonly AuditEventClass[] SecurityClasses = [AuditEventClass.Authentication, AuditEventClass.AuthorizationDenial];
    private static readonly AuditEventClass[] AdministrationClasses = [AuditEventClass.PrivilegedAction, AuditEventClass.PermissionChange, AuditEventClass.ConfigurationChange];
    private static readonly AuditEventClass[] BusinessClasses = [AuditEventClass.LifecycleTransition, AuditEventClass.DataChange, AuditEventClass.ApprovalDecision];

    public static IReadOnlyList<AuditCategory> All { get; } = Enum.GetValues<AuditCategory>();

    public static AuditCategory Of(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return SecurityClasses.Contains(auditEvent.EventClass) ? AuditCategory.Security
            : AdministrationClasses.Contains(auditEvent.EventClass) ? AuditCategory.Administration
            : auditEvent.EventClass == AuditEventClass.Integration ? AuditCategory.Integration
            : auditEvent.EventType.StartsWith(FinancialProducer, StringComparison.Ordinal) ? AuditCategory.Financial
            : auditEvent.EventType.StartsWith(ExternalProducer, StringComparison.Ordinal) ? AuditCategory.External
            : AuditCategory.Business;
    }

    /// <summary>The events of <paramref name="category"/>, as a condition the database evaluates; the same partition as <see cref="Of"/>.</summary>
    public static Expression<Func<AuditEvent, bool>> In(AuditCategory category) => category switch
    {
        AuditCategory.Security => e => SecurityClasses.Contains(e.EventClass),
        AuditCategory.Administration => e => AdministrationClasses.Contains(e.EventClass),
        AuditCategory.Integration => e => e.EventClass == AuditEventClass.Integration,
        AuditCategory.Financial => e => BusinessClasses.Contains(e.EventClass) && e.EventType.StartsWith(FinancialProducer),
        AuditCategory.External => e => BusinessClasses.Contains(e.EventClass) && e.EventType.StartsWith(ExternalProducer),
        AuditCategory.Business => e => BusinessClasses.Contains(e.EventClass) && !e.EventType.StartsWith(FinancialProducer) && !e.EventType.StartsWith(ExternalProducer),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown audit category."),
    };

    public static string PermissionOf(AuditCategory category) => category switch
    {
        AuditCategory.Security => PermissionCatalogue.AuditSecurityView,
        AuditCategory.Administration => PermissionCatalogue.AuditAdministrationView,
        AuditCategory.Integration => PermissionCatalogue.AuditIntegrationView,
        AuditCategory.Financial => PermissionCatalogue.AuditFinancialView,
        AuditCategory.External => PermissionCatalogue.AuditExternalView,
        AuditCategory.Business => PermissionCatalogue.AuditBusinessView,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown audit category."),
    };
}
