namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>How a stored Activity line differs from what the audit store derives (TASK-073 rebuild-and-diff).</summary>
public enum ActivityDifferenceKind
{
    /// <summary>The store derives a line the projection does not hold.</summary>
    Missing = 1,

    /// <summary>The projection holds a line the store does not derive.</summary>
    Unexpected = 2,

    /// <summary>Both have the line and its content differs.</summary>
    Changed = 3,
}

public sealed record ActivityDifference(Guid AuditEventId, ActivityDifferenceKind Kind);

/// <summary>
/// The Activity projection compared with what the audit store derives (AUD-API-035). A check compares the events up to the
/// projection's checkpoint and reports the events after it as <see cref="PendingEvents"/>, the expected lag; a rebuild compares
/// every event, repairs each difference and moves the checkpoint to the store's head. <see cref="Differences"/> lists at most
/// <see cref="MaxListedDifferences"/>; the three counts are complete.
/// </summary>
public sealed record ActivityProjectionReport(
    bool Rebuilt,
    int EventsCompared,
    int EntriesExpected,
    int Missing,
    int Unexpected,
    int Changed,
    int PendingEvents,
    DateTimeOffset? CheckpointRecordedAt,
    IReadOnlyList<ActivityDifference> Differences)
{
    public const int MaxListedDifferences = 100;

    /// <summary>True when the compared events derive exactly the stored lines.</summary>
    public bool Matches => Missing == 0 && Unexpected == 0 && Changed == 0;
}
