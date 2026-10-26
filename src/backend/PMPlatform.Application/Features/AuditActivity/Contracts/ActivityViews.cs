namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>
/// One line of a project's Activity timeline (SCR-057; FG-06 §7.1): what happened, when, in which domain, and by whom when the
/// caller may know. An external viewer sees the actor only when the actor belongs to their own entity (§7.4); otherwise
/// <see cref="ActorUserId"/> and <see cref="ActorDisplayName"/> are null. The subject is a reference for a drill-through that the
/// source module authorises again (AUD-CC-17); it carries no source data.
/// </summary>
public sealed record ActivityEntryView(
    Guid Id,
    Guid AuditEventId,
    Guid? ProjectId,
    DateTimeOffset OccurredAt,
    string ActivityKind,
    string EventType,
    string SummaryAr,
    string SummaryEn,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string? SubjectModule,
    string? SubjectType,
    Guid? SubjectId);

public sealed record ActivityEntryPage(IReadOnlyList<ActivityEntryView> Items, int Page, int PageSize, int TotalCount);

/// <summary>The timeline's filters (FG-06 §7.2): project, source record, domain, actor and time. Newest first.</summary>
public sealed record ActivityQuery
{
    public Guid? ProjectId { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public IReadOnlyCollection<string> ActivityKinds { get; init; } = [];

    public Guid? ActorUserId { get; init; }

    public DateTimeOffset? OccurredFrom { get; init; }

    public DateTimeOffset? OccurredTo { get; init; }
}
