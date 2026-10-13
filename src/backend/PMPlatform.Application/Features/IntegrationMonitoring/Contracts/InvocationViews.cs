using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>One attempt as ADM-048 lists it: codes, times and correlation; never a payload, a secret or a provider's message.</summary>
public sealed record InvocationSummary(
    Guid Id,
    Guid IntegrationInstanceId,
    string IntegrationCode,
    Guid? SyncRunId,
    string OperationCode,
    Guid CorrelationId,
    string? IdempotencyKey,
    short AttemptNo,
    string? SourceModule,
    string? SourceReference,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    InvocationStatus Status,
    InvocationFailureCategory? FailureCategory,
    string? ResponseCode,
    string? ErrorSummary,
    DateTimeOffset? NextAttemptAt);

public sealed record InvocationPage(IReadOnlyList<InvocationSummary> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// An attempt with every attempt of its lineage, oldest first (FG-05 US-INT-OPS-003), and whether FG-05 would retry the lineage now: the
/// backend decides, the screen shows it (FG-05 §20.1).
/// </summary>
public sealed record InvocationDetail(InvocationSummary Invocation, IReadOnlyList<InvocationSummary> Lineage, bool RetryPermitted);

/// <summary>ADM-048's filters (FG-05 §13.2): by integration, state, operation, correlation id, lineage key, business reference and time. Newest first.</summary>
public sealed record InvocationQuery
{
    public Guid? IntegrationId { get; init; }

    public IReadOnlyCollection<InvocationStatus> Statuses { get; init; } = [];

    public string? OperationCode { get; init; }

    public Guid? CorrelationId { get; init; }

    public string? IdempotencyKey { get; init; }

    public string? SourceReference { get; init; }

    public DateTimeOffset? StartedFrom { get; init; }

    public DateTimeOffset? StartedTo { get; init; }
}
