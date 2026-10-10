namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>The Reports module's error codes (api-conventions R-27; FG-02 §23). A code, once shipped, keeps its meaning.</summary>
public static class ReportErrorCodes
{
    /// <summary>422: a version fails ADM-037's validation (RPT-E004); <c>errors[]</c> names each field.</summary>
    public const string DefinitionInvalid = "REPORT_DEFINITION_INVALID";

    /// <summary>422: author, reviewer and publisher differ (GovernedLifecycle; TBC-RPT-05).</summary>
    public const string SeparationOfDuties = "REPORT_SEPARATION_OF_DUTIES";

    /// <summary>409: the report already has a version on its way, DRAFT or VALIDATED.</summary>
    public const string VersionOpen = "REPORT_VERSION_OPEN";

    /// <summary>409: a PUBLISHED version is replaced by publishing its successor, never retired alone: the ten reports stay ten (ADR-006).</summary>
    public const string RetirementNotPermitted = "REPORT_RETIREMENT_NOT_PERMITTED";

    /// <summary>422: a parameter is unknown, repeated, of the wrong type or not one of its options (RPT-E010).</summary>
    public const string ParameterInvalid = "REPORT_PARAMETER_INVALID";

    /// <summary>422: a required parameter has no value (RPT-E011).</summary>
    public const string ParameterRequired = "REPORT_PARAMETER_REQUIRED";

    /// <summary>422: a project or department the caller may not know, the same answer for one that does not exist (RPT-E012, BR-RPT-010).</summary>
    public const string ParameterUnauthorized = "REPORT_PARAMETER_UNAUTHORIZED";

    /// <summary>
    /// 422: a column the report does not publish, or — in the SCR-138 explorer — a field or a projection the REPORT_RULES allowlist does not name
    /// (RPT-E016; acceptance criterion 1 of TASK-071). Nothing outside the allowlist is ever read.
    /// </summary>
    public const string ColumnNotSupported = "REPORT_COLUMN_NOT_SUPPORTED";

    /// <summary>422: a filter on a field that is not filterable there, or with an operator its type does not take (RPT-E013).</summary>
    public const string FilterNotSupported = "REPORT_FILTER_NOT_SUPPORTED";

    /// <summary>422: a filter value that is not a value of its field (RPT-E014).</summary>
    public const string FilterValueInvalid = "REPORT_FILTER_VALUE_INVALID";

    /// <summary>422: a sort on a field that is not sortable there (RPT-E018).</summary>
    public const string SortNotSupported = "REPORT_SORT_NOT_SUPPORTED";

    /// <summary>422: the report does not allow saved views (REP-010), or the view names a report the caller may not run.</summary>
    public const string SavedViewNotAllowed = "REPORT_SAVED_VIEW_NOT_ALLOWED";

    /// <summary>422: a saved view no longer fits the report version or the allowlist in force (RPT-E048); <c>errors[]</c> names what changed.</summary>
    public const string SavedViewIncompatible = "REPORT_SAVED_VIEW_INCOMPATIBLE";

    /// <summary>422: the same <c>Idempotency-Key</c> sent with a different request (RPT-E057, R-37).</summary>
    public const string DuplicateRequest = "REPORT_DUPLICATE_REQUEST";

    /// <summary>409: the job is past cancelling: COMPLETED, FAILED, CANCELLED or EXPIRED (RPT-E039).</summary>
    public const string JobNotCancellable = "REPORT_JOB_NOT_CANCELLABLE";

    /// <summary>409: the job has produced no output: not COMPLETED yet, or FAILED or CANCELLED (RPT-E033).</summary>
    public const string OutputNotAvailable = "REPORT_OUTPUT_NOT_AVAILABLE";

    /// <summary>409: the output has expired and is no longer downloadable; the job's record stays (RPT-E034).</summary>
    public const string OutputExpired = "REPORT_OUTPUT_EXPIRED";

    // The safe failure codes a job may end with (FG-02 REP-011): a code, never a message.

    /// <summary>The requester no longer holds the export permission, or no longer has an active account (RPT-E029).</summary>
    public const string ExportNotPermitted = "REPORT_EXPORT_NOT_PERMITTED";

    /// <summary>The requester is no longer of the report's audience (RPT-E002).</summary>
    public const string AccessDenied = "REPORT_ACCESS_DENIED";

    /// <summary>The report version requested is no longer the one in force (RPT-E003).</summary>
    public const string NotActive = "REPORT_NOT_ACTIVE";

    /// <summary>The request has more rows than an export may hold (RPT-E037; TBC-RPT-10).</summary>
    public const string ExportSizeLimitExceeded = "REPORT_EXPORT_SIZE_LIMIT_EXCEEDED";

    /// <summary>The output could not be rendered or stored (RPT-E031, RPT-E032).</summary>
    public const string OutputGenerationFailed = "REPORT_OUTPUT_GENERATION_FAILED";

    /// <summary>A download refused because the requester may no longer see what the output holds (RPT-E035): an audit reason, answered 403.</summary>
    public const string DownloadAccessDenied = "REPORT_DOWNLOAD_ACCESS_DENIED";
}

/// <summary>The <c>errors[].code</c> values Reports answers with beside <see cref="IdentityAccess.Contracts.Administration.FieldIssue"/>'s.</summary>
public static class ReportIssueCodes
{
    /// <summary>A field the allowlist does not name, of a projection it does name.</summary>
    public const string FieldNotAllowlisted = "FIELD_NOT_ALLOWLISTED";

    /// <summary>A projection the allowlist does not name at all: a join it does not allow (FG-02 §11.3).</summary>
    public const string JoinNotAllowlisted = "JOIN_NOT_ALLOWLISTED";

    /// <summary>A field the report version does not publish.</summary>
    public const string FieldNotInReport = "FIELD_NOT_IN_REPORT";

    /// <summary>A field of the register that is not of the row's grain: a project's value beside a snapshot's, or the reverse.</summary>
    public const string GrainMismatch = "GRAIN_MISMATCH";

    /// <summary>A projection or field not in FG-01's register (BR-RPT-003).</summary>
    public const string ProjectionNotRegistered = "PROJECTION_NOT_REGISTERED";

    /// <summary>A role the report may not be offered to: R01 (BR-RPT-011), or R08 outside the entity report set (ADR-013).</summary>
    public const string AudienceNotPermitted = "AUDIENCE_NOT_PERMITTED";

    /// <summary>An operator the field's type does not take.</summary>
    public const string OperatorNotSupported = "OPERATOR_NOT_SUPPORTED";

    /// <summary>A value that is not of the field's type or closed set.</summary>
    public const string ValueInvalid = "VALUE_INVALID";
}
