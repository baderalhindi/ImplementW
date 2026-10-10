using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// A report job (SCR-140, RPT-API-010): what was asked, by whom and when, how far it has come, and its output once there is one.
/// <see cref="FailureCode"/> is a code from the error catalogue, never a message (FG-02 REP-011).
/// </summary>
public sealed record ReportJobDetail(
    Guid Id,
    ReportJobKind Kind,
    ReportCode? ReportCode,
    Guid? ReportDefinitionId,
    ReportExportFormat ExportFormat,
    Language ReportLanguage,
    ReportJobStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    Guid CorrelationId,
    GeneratedOutputDetail? Output);

/// <summary>A job's output: its file, integrity hash, classification, the least current as-of of what it holds, and its expiry (GEN-005 to -013).</summary>
public sealed record GeneratedOutputDetail(
    string FileName,
    string ContentType,
    long SizeBytes,
    string ChecksumSha256,
    OutputSensitivity Sensitivity,
    int RowCount,
    DateTimeOffset? SourceAsOf,
    DateTimeOffset ExpiresAt,
    GeneratedOutputStatus Status);

/// <summary>A job in the caller's Export History (SCR-140).</summary>
public sealed record ReportJobSummary(
    Guid Id,
    ReportJobKind Kind,
    ReportCode? ReportCode,
    ReportExportFormat ExportFormat,
    ReportJobStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    OutputSensitivity? Sensitivity,
    DateTimeOffset? ExpiresAt);

public sealed record ReportJobPage(IReadOnlyList<ReportJobSummary> Items, int Page, int PageSize, int TotalCount);

/// <summary>An export request's answer: the job, and whether it was already made for the same key (R-39).</summary>
public sealed record ReportExportOutcome(ReportJobDetail Job, bool Replayed);

/// <summary>An authorised download: the bytes, checked against their hash, and the name and type to send them under. The caller disposes the stream.</summary>
public sealed record ReportOutputDownload(Stream Content, string FileName, string ContentType);
