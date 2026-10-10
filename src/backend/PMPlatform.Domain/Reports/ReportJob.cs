using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// One asynchronous generation of a downloadable output (FG-02 §10.1, ReportJob REP-001–015): every PDF, XLSX and CSV is produced by one, so each
/// is reauthorised, classified, audited and expired the same way (DC-RPT-01). <see cref="RequestSnapshot"/> is the validated request — report
/// version, parameters, columns, filters and sorts — as it was accepted: the job runs that request and nothing else. Delete policy: RETAIN.
/// </summary>
public sealed class ReportJob : AuditedEntity
{
    public ReportJobKind Kind { get; set; }

    /// <summary>The exact report version requested (REPORT jobs only).</summary>
    public Guid? ReportDefinitionId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public ReportExportFormat ExportFormat { get; set; }

    /// <summary>The language the output is rendered in; a project's narrative stays in the language entered (ADR-012).</summary>
    public Language ReportLanguage { get; set; }

    public Guid CorrelationId { get; set; }

    /// <summary>The requester's key for the request (US-RPT-SYS-038): a repeated request answers with this job, never a second one.</summary>
    public Guid IdempotencyKey { get; set; }

    public ReportJobStatus Status { get; set; }

    /// <summary>The validated request, as JSON.</summary>
    public required string RequestSnapshot { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>A code from the error catalogue, never a message, a stack trace or a value (FG-02 REP-011, RPT-E040).</summary>
    public string? FailureCode { get; set; }
}
