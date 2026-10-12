using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.AuditActivity;

/// <summary>
/// How far the Activity projection has read the audit store (TASK-073): the last event it considered, in the store's
/// <c>(recorded_at, id)</c> order. Events are committed in that order, so every event at or before the checkpoint has been
/// projected or deliberately left out. One row. Delete policy: RETAIN.
/// </summary>
public sealed class ActivityProjectionCheckpoint : AuditedEntity
{
    /// <summary>The id of the one row.</summary>
    public static readonly Guid SingletonId = new("00000000-0073-4000-8000-000000000001");

    public DateTimeOffset? LastRecordedAt { get; set; }

    public Guid? LastAuditEventId { get; set; }
}
