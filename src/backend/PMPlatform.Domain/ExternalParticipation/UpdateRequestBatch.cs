using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// One sending of monthly update requests, one per project (the 5 Nov 2026 review §2.2), tracked together. A repeating batch names the day of the month the next one goes
/// out; each sending of a series is its own batch.
/// </summary>
public sealed class UpdateRequestBatch : AuditedEntity
{
    /// <summary>The series a repeating batch belongs to: the first batch's id.</summary>
    public Guid SeriesId { get; set; }

    public DateOnly DueDate { get; set; }

    public required NarrativeText Instructions { get; set; }

    public Guid ReviewerUserId { get; set; }

    public bool RepeatMonthly { get; set; }

    /// <summary>The day of the month the next batch goes out, 1 to 28.</summary>
    public int? RepeatDay { get; set; }

    /// <summary>When the next batch of the series is due out; null once it has gone, or when it does not repeat.</summary>
    public DateOnly? NextRunOn { get; set; }

    public DateTimeOffset? WithdrawnAt { get; set; }
}
