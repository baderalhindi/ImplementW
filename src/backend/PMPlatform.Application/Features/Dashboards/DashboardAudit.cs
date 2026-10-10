using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Dashboards.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>The audit events of FG-01's configuration and personalisation (FG-01 §27; TASK-033 <c>IAuditTrail</c>). Labels are not copied.</summary>
internal static class DashboardAudit
{
    public const string SubjectModule = "Dashboards";
    public const string DefinitionType = "DashboardDefinition";
    public const string PreferenceType = "UserDashboardPreference";

    public static AuditEntry Created(Guid actorId, DashboardDefinition definition, IReadOnlyList<DashboardWidget> widgets) =>
        Entry(AuditEventClass.DataChange, DashboardAuditEvents.DefinitionCreated, actorId, definition,
        [
            AuditAttribute.Of(DashboardAuditAttributes.Code, definition.Code),
            AuditAttribute.Of(DashboardAuditAttributes.VersionNo, definition.VersionNo),
            AuditAttribute.Of(DashboardAuditAttributes.WidgetCount, widgets.Count),
        ]);

    public static AuditEntry Changed(Guid actorId, DashboardDefinition definition, IReadOnlyList<DashboardWidget> widgets, IReadOnlyList<string> audienceRoleCodes) =>
        Entry(AuditEventClass.DataChange, DashboardAuditEvents.DefinitionChanged, actorId, definition,
        [
            AuditAttribute.Of(DashboardAuditAttributes.Code, definition.Code),
            AuditAttribute.Of(DashboardAuditAttributes.VersionNo, definition.VersionNo),
            AuditAttribute.Of(DashboardAuditAttributes.WidgetCount, widgets.Count),
            AuditAttribute.Of(DashboardAuditAttributes.ProjectionCodes, string.Join(',', widgets.Select(w => w.SourceProjectionCode).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))),
            AuditAttribute.Of(DashboardAuditAttributes.AudienceRoleCodes, string.Join(',', audienceRoleCodes.Order(StringComparer.Ordinal))),
        ]);

    public static AuditEntry Validated(Guid actorId, DashboardDefinition definition) =>
        Transition(DashboardAuditEvents.DefinitionValidated, actorId, definition, GovernedLifecycleState.Draft, []);

    public static AuditEntry Published(Guid actorId, DashboardDefinition definition, Guid? supersededId) =>
        Transition(DashboardAuditEvents.DefinitionPublished, actorId, definition, GovernedLifecycleState.Validated,
            [AuditAttribute.Of(DashboardAuditAttributes.SupersededDefinitionId, supersededId)]);

    public static AuditEntry Retired(Guid actorId, DashboardDefinition definition, GovernedLifecycleState from, Guid? supersededById) =>
        Transition(DashboardAuditEvents.DefinitionRetired, actorId, definition, from,
            [AuditAttribute.Of(DashboardAuditAttributes.SupersededByDefinitionId, supersededById)]);

    public static AuditEntry PersonalizationChanged(Guid actorId, UserDashboardPreference preference, DashboardDefinition definition, IReadOnlyList<string> hiddenWidgetCodes) =>
        Preference(DashboardAuditEvents.PersonalizationChanged, actorId, preference, definition,
            [AuditAttribute.Of(DashboardAuditAttributes.HiddenWidgetCodes, string.Join(',', hiddenWidgetCodes.Order(StringComparer.Ordinal)))]);

    public static AuditEntry PersonalizationReset(Guid actorId, UserDashboardPreference preference, DashboardDefinition definition) =>
        Preference(DashboardAuditEvents.PersonalizationReset, actorId, preference, definition, []);

    private static AuditEntry Transition(string eventType, Guid actorId, DashboardDefinition definition, GovernedLifecycleState from, IEnumerable<AuditAttribute> attributes) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, definition,
        [
            AuditAttribute.Change(DashboardAuditAttributes.LifecycleState, from, definition.LifecycleState)!,
            AuditAttribute.Of(DashboardAuditAttributes.Code, definition.Code),
            AuditAttribute.Of(DashboardAuditAttributes.VersionNo, definition.VersionNo),
            .. attributes,
        ]);

    private static AuditEntry Entry(AuditEventClass eventClass, string eventType, Guid actorId, DashboardDefinition definition, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(SubjectModule, DefinitionType, definition.Id),
            Attributes = [.. attributes],
        };

    private static AuditEntry Preference(string eventType, Guid actorId, UserDashboardPreference preference, DashboardDefinition definition, IEnumerable<AuditAttribute> attributes) =>
        new(AuditEventClass.DataChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(SubjectModule, PreferenceType, preference.Id),
            Attributes =
            [
                AuditAttribute.Of(DashboardAuditAttributes.Code, definition.Code),
                AuditAttribute.Of(DashboardAuditAttributes.VersionNo, definition.VersionNo),
                .. attributes,
            ],
        };
}
