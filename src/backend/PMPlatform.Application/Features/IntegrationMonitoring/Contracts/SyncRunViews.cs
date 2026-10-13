using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>A synchronisation or reconciliation run and its counts (FG-05 §6.4). A reconciliation applies nothing: <c>RecordsApplied</c> is 0.</summary>
public sealed record SyncRunView(
    Guid Id,
    Guid IntegrationInstanceId,
    string IntegrationCode,
    SyncRunType RunType,
    Guid CorrelationId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    SyncRunStatus Status,
    int RecordsRead,
    int RecordsApplied,
    int RecordsRejected,
    int MismatchCount);

public sealed record SyncRunPage(IReadOnlyList<SyncRunView> Items, int Page, int PageSize, int TotalCount);

public sealed record SyncRunQuery
{
    public Guid? IntegrationId { get; init; }

    public IReadOnlyCollection<SyncRunType> RunTypes { get; init; } = [];

    public IReadOnlyCollection<SyncRunStatus> Statuses { get; init; } = [];

    public Guid? CorrelationId { get; init; }
}
