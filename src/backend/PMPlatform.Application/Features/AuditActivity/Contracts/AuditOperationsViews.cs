namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>
/// The audit pipeline's health (AUD-API-039, -040; FG-06 §24.4): what is captured and not yet stored, what keeps failing, and how
/// far the Activity projection trails the store. Counts and times only, never an event's content.
/// </summary>
public sealed record AuditIngestionStatus(
    int PendingMessages,
    int FailingMessages,
    DateTimeOffset? OldestPendingOccurredAt,
    string? LastError,
    int EventsStored,
    DateTimeOffset? StoreHeadRecordedAt,
    int ProjectionLag,
    DateTimeOffset? ProjectionCheckpointRecordedAt);

public enum AuditIntegrityFindingKind
{
    /// <summary>The event's hash is not the hash of its row, or its previous hash is not its predecessor's: it, or the event before it, was changed or removed.</summary>
    BrokenLink = 1,

    /// <summary>The event's attributes no longer have the digest stored with it: one was changed, added or removed.</summary>
    AttributeDigestMismatch = 2,
}

public sealed record AuditIntegrityFinding(Guid AuditEventId, AuditIntegrityFindingKind Kind);

/// <summary>
/// The whole chain checked (AUD-API-037; AUD-CC-27). <see cref="EventsWithoutDigest"/> are events stored before TASK-073, whose
/// attributes the chain does not cover. <see cref="Findings"/> lists at most <see cref="MaxListedFindings"/>; the counts are complete.
/// </summary>
public sealed record AuditIntegrityReport(
    int EventsVerified,
    int BrokenLinks,
    int AttributeDigestMismatches,
    int EventsWithoutDigest,
    IReadOnlyList<AuditIntegrityFinding> Findings)
{
    public const int MaxListedFindings = 100;

    public bool Intact => BrokenLinks == 0 && AttributeDigestMismatches == 0;
}
