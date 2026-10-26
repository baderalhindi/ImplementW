namespace PMPlatform.Application.Features.DocumentManagement.Contracts.Events;

/// <summary>
/// The audit event types DocumentManagement produces (TASK-037; event-conventions EV-1), recorded through
/// <c>IAuditTrail</c> in the producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class DocumentAuditEvents
{
    /// <summary>DATA_CHANGE: a document and its first version.</summary>
    public const string DocumentUploaded = "DocumentManagement.DocumentUploaded";

    /// <summary>DATA_CHANGE.</summary>
    public const string VersionAdded = "DocumentManagement.VersionAdded";

    /// <summary>DATA_CHANGE: title, description, type or classification.</summary>
    public const string MetadataChanged = "DocumentManagement.MetadataChanged";

    /// <summary>LIFECYCLE_TRANSITION.</summary>
    public const string DocumentArchived = "DocumentManagement.DocumentArchived";

    /// <summary>LIFECYCLE_TRANSITION: SCAN_PENDING → CLEAN, QUARANTINED or SCAN_FAILED, by the scan service.</summary>
    public const string ScanCompleted = "DocumentManagement.ScanCompleted";

    /// <summary>LIFECYCLE_TRANSITION: SCAN_FAILED → SCAN_PENDING.</summary>
    public const string ScanRequeued = "DocumentManagement.ScanRequeued";

    /// <summary>AUTHORIZATION_DENIAL: the content of a QUARANTINED version was asked for and withheld.</summary>
    public const string QuarantinedContentWithheld = "DocumentManagement.QuarantinedContentWithheld";

    /// <summary>DATA_CHANGE.</summary>
    public const string DocumentLinked = "DocumentManagement.DocumentLinked";

    /// <summary>DATA_CHANGE: the link ended; nothing was deleted.</summary>
    public const string DocumentUnlinked = "DocumentManagement.DocumentUnlinked";

    /// <summary>DATA_CHANGE.</summary>
    public const string EvidenceDesignated = "DocumentManagement.EvidenceDesignated";

    /// <summary>DATA_CHANGE.</summary>
    public const string EvidenceWithdrawn = "DocumentManagement.EvidenceWithdrawn";
}
