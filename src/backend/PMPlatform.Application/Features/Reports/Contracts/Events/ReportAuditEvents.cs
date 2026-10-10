namespace PMPlatform.Application.Features.Reports.Contracts.Events;

/// <summary>
/// The audit event types Reports produces (TASK-071; FG-02 §24 RPT-EVT; event-conventions EV-1), recorded through <c>IAuditTrail</c>. Routine
/// on-screen runs are not audited; a run that reveals sensitive values is (FG-02 §24.1, RPT-EVT-003).
/// </summary>
public static class ReportAuditEvents
{
    /// <summary>RPT-EVT-003: an on-screen run revealed a financial amount or a classified column.</summary>
    public const string SensitiveReportExecuted = "Reports.SensitiveReportExecuted";

    /// <summary>RPT-EVT-005/006: an export was requested and its job accepted.</summary>
    public const string ExportRequested = "Reports.ExportRequested";

    /// <summary>RPT-EVT-008: the output was generated, classified and stored.</summary>
    public const string ExportCompleted = "Reports.ExportCompleted";

    /// <summary>RPT-EVT-009: the job ended without an output, with its safe failure code.</summary>
    public const string ExportFailed = "Reports.ExportFailed";

    /// <summary>RPT-EVT-010: the requester cancelled the job.</summary>
    public const string ExportCancelled = "Reports.ExportCancelled";

    /// <summary>RPT-EVT-011: the output was downloaded.</summary>
    public const string OutputDownloaded = "Reports.OutputDownloaded";

    /// <summary>RPT-EVT-012: the output expired and its file was purged.</summary>
    public const string OutputExpired = "Reports.OutputExpired";

    /// <summary>RPT-EVT-013: a download was refused because its requester may no longer see what it holds.</summary>
    public const string OutputAccessDenied = "Reports.OutputAccessDenied";

    /// <summary>RPT-EVT-014: a saved view was created.</summary>
    public const string SavedViewCreated = "Reports.SavedViewCreated";

    /// <summary>RPT-EVT-015: a saved view was replaced or deleted.</summary>
    public const string SavedViewChanged = "Reports.SavedViewChanged";

    /// <summary>RPT-EVT-028: a version was opened as a DRAFT.</summary>
    public const string DefinitionCreated = "Reports.DefinitionCreated";

    /// <summary>RPT-EVT-029: a DRAFT's content was replaced.</summary>
    public const string DefinitionChanged = "Reports.DefinitionChanged";

    /// <summary>RPT-EVT-030: DRAFT → VALIDATED.</summary>
    public const string DefinitionValidated = "Reports.DefinitionValidated";

    /// <summary>RPT-EVT-032: VALIDATED → PUBLISHED, with the version it superseded.</summary>
    public const string DefinitionPublished = "Reports.DefinitionPublished";

    /// <summary>RPT-EVT-033: → RETIRED, abandoned or superseded.</summary>
    public const string DefinitionRetired = "Reports.DefinitionRetired";
}

/// <summary>The attribute names of Reports' audit events. Values are codes, counts and ids: never a report's data.</summary>
public static class ReportAuditAttributes
{
    public const string Code = "code";
    public const string VersionNo = "version_no";
    public const string LifecycleState = "lifecycle_state";
    public const string Status = "status";
    public const string Kind = "kind";
    public const string ExportFormat = "export_format";
    public const string ReportLanguage = "report_language";
    public const string Columns = "columns";
    public const string ColumnCount = "column_count";
    public const string ParameterCount = "parameter_count";
    public const string AudienceRoleCodes = "audience_role_codes";
    public const string RowCount = "row_count";
    public const string ProjectCount = "project_count";
    public const string Sensitivity = "sensitivity";
    public const string SizeBytes = "size_bytes";
    public const string ChecksumSha256 = "checksum_sha256";
    public const string NeutralizedCellCount = "neutralized_cell_count";
    public const string FailureCode = "failure_code";
    public const string ReasonCode = "reason_code";
    public const string SupersededDefinitionId = "superseded_definition_id";
    public const string SupersededByDefinitionId = "superseded_by_definition_id";
    public const string ViewType = "view_type";
}
