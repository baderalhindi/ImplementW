namespace PMPlatform.Application.Features.Dashboards.Contracts;

/// <summary>The Dashboards module's error codes (api-conventions R-27; FG-01 §26). A code, once shipped, keeps its meaning.</summary>
public static class DashboardErrorCodes
{
    /// <summary>422: a version fails structural, dependency or security validation (ERR-DSH-004); <c>errors[]</c> names each widget or field.</summary>
    public const string DefinitionInvalid = "DASHBOARD_DEFINITION_INVALID";

    /// <summary>422: author, reviewer and publisher differ (GovernedLifecycle; TBC-DSH-024).</summary>
    public const string SeparationOfDuties = "DASHBOARD_SEPARATION_OF_DUTIES";

    /// <summary>409: the dashboard already has a version on its way, DRAFT or VALIDATED.</summary>
    public const string VersionOpen = "DASHBOARD_VERSION_OPEN";

    /// <summary>409: a PUBLISHED version is replaced by publishing its successor, never retired alone: the three dashboards stay (ADR-006).</summary>
    public const string RetirementNotPermitted = "DASHBOARD_RETIREMENT_NOT_PERMITTED";

    /// <summary>422: the dashboard needs a context this request does not give, or gives one it does not take (ERR-DSH-033).</summary>
    public const string ContextInvalid = "DASHBOARD_CONTEXT_INVALID";

    /// <summary>422: a filter value is not one the caller may know (ERR-DSH-032); the same answer for one that does not exist.</summary>
    public const string FilterValueUnauthorized = "DASHBOARD_FILTER_VALUE_UNAUTHORIZED";

    /// <summary>422: the dashboard does not allow personalisation, or a widget named is not optional (ERR-DSH-050/051).</summary>
    public const string PersonalizationInvalid = "DASHBOARD_PERSONALIZATION_INVALID";
}

/// <summary>The <c>errors[].code</c> values a definition's validation answers with (FG-01 §13.4), beside <see cref="IdentityAccess.Contracts.Administration.FieldIssue"/>'s.</summary>
public static class DashboardIssueCodes
{
    /// <summary>The widget names no registered projection (BR-DSH-029, SEC-DSH-08).</summary>
    public const string ProjectionNotRegistered = "PROJECTION_NOT_REGISTERED";

    /// <summary>The projection does not support this widget type (ERR-DSH-013).</summary>
    public const string WidgetTypeNotSupported = "WIDGET_TYPE_NOT_SUPPORTED";

    /// <summary>The projection does not support the dashboard's context: one project, or a population of projects (ERR-DSH-022).</summary>
    public const string ContextNotSupported = "CONTEXT_NOT_SUPPORTED";

    /// <summary>Two widgets overlap on the grid (ERR-DSH-014).</summary>
    public const string LayoutOverlap = "LAYOUT_OVERLAP";

    /// <summary>A role the dashboard may not be offered to (ADR-019: entity users get the Project Dashboard only).</summary>
    public const string AudienceNotPermitted = "AUDIENCE_NOT_PERMITTED";

    /// <summary>A role is already the default landing of another PUBLISHED dashboard (Blueprint §20.2).</summary>
    public const string DefaultLandingTaken = "DEFAULT_LANDING_TAKEN";
}
