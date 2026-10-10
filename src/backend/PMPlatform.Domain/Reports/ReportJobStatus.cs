namespace PMPlatform.Domain.Reports;

/// <summary>
/// A report job's lifecycle (ERD §6; api-conventions R-7): REQUESTED → VALIDATING → QUEUED → RUNNING → COMPLETED, FAILED or CANCELLED; a
/// COMPLETED job's output expires (EXPIRED). FAILED, CANCELLED and EXPIRED are terminal.
/// </summary>
public enum ReportJobStatus
{
    /// <summary>Accepted from its requester, not yet checked again.</summary>
    Requested = 1,

    /// <summary>Its requester's access, the report version and the allowlist are being checked again before any data is read (US-RPT-SYS-026).</summary>
    Validating = 2,

    /// <summary>Checked, waiting to run.</summary>
    Queued = 3,

    /// <summary>Its rows are read and rendered.</summary>
    Running = 4,

    /// <summary>Its output is stored and may be downloaded until it expires.</summary>
    Completed = 5,

    /// <summary>Finished without an output; the safe failure code says why. No partial file is kept.</summary>
    Failed = 6,

    /// <summary>Cancelled by its requester; no partial file is kept.</summary>
    Cancelled = 7,

    /// <summary>Its output expired and was purged; the job's record stays.</summary>
    Expired = 8,
}
