namespace PMPlatform.Application.Features.Dashboards.Contracts.Events;

/// <summary>
/// The audit event types Dashboards produces (TASK-069; FG-01 §27 EVT-DSH-001 to -024; event-conventions EV-1), recorded through
/// <c>IAuditTrail</c> in the producer's unit of work. Widget loads and views are operational telemetry, not audit (FG-01 §27.1).
/// </summary>
public static class DashboardAuditEvents
{
    /// <summary>DATA_CHANGE: a version was opened as a DRAFT (EVT-DSH-001).</summary>
    public const string DefinitionCreated = "Dashboards.DefinitionCreated";

    /// <summary>DATA_CHANGE: a DRAFT's content was replaced (EVT-DSH-002 to -007), with its widget and audience counts.</summary>
    public const string DefinitionChanged = "Dashboards.DefinitionChanged";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT → VALIDATED (EVT-DSH-008).</summary>
    public const string DefinitionValidated = "Dashboards.DefinitionValidated";

    /// <summary>LIFECYCLE_TRANSITION: VALIDATED → PUBLISHED, with the version it superseded (EVT-DSH-010).</summary>
    public const string DefinitionPublished = "Dashboards.DefinitionPublished";

    /// <summary>LIFECYCLE_TRANSITION: → RETIRED, abandoned or superseded by a newer version (EVT-DSH-012, EVT-DSH-035).</summary>
    public const string DefinitionRetired = "Dashboards.DefinitionRetired";

    /// <summary>DATA_CHANGE: a person's personalisation of a dashboard was replaced (EVT-DSH-022).</summary>
    public const string PersonalizationChanged = "Dashboards.PersonalizationChanged";

    /// <summary>DATA_CHANGE: a person's personalisation was reset to the governed layout (EVT-DSH-023).</summary>
    public const string PersonalizationReset = "Dashboards.PersonalizationReset";
}

/// <summary>The attribute names of Dashboards' audit events.</summary>
public static class DashboardAuditAttributes
{
    public const string Code = "code";
    public const string VersionNo = "version_no";
    public const string LifecycleState = "lifecycle_state";
    public const string WidgetCount = "widget_count";
    public const string AudienceRoleCodes = "audience_role_codes";
    public const string ProjectionCodes = "projection_codes";
    public const string SupersededDefinitionId = "superseded_definition_id";
    public const string SupersededByDefinitionId = "superseded_by_definition_id";
    public const string HiddenWidgetCodes = "hidden_widget_codes";
}
