using PMPlatform.Domain.Common;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>An operational alert (FG-05 §10): the condition, where it was found, and the operator's handling, apart from the integration's health.</summary>
public sealed record OperationalAlertView(
    Guid Id,
    Guid IntegrationInstanceId,
    string IntegrationCode,
    Guid? SyncRunId,
    Guid? InvocationId,
    OperationalAlertType AlertType,
    OperationalAlertSeverity Severity,
    DateTimeOffset RaisedAt,
    OperationalAlertStatus Status,
    Guid? AcknowledgedByUserId,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    NarrativeText? ResolutionNote,
    string? Detail);

public sealed record OperationalAlertPage(IReadOnlyList<OperationalAlertView> Items, int Page, int PageSize, int TotalCount);

/// <summary>ADM-047's alert filters. Newest first.</summary>
public sealed record OperationalAlertQuery
{
    public Guid? IntegrationId { get; init; }

    public IReadOnlyCollection<OperationalAlertStatus> Statuses { get; init; } = [];

    public IReadOnlyCollection<OperationalAlertType> AlertTypes { get; init; } = [];

    public DateTimeOffset? RaisedFrom { get; init; }

    public DateTimeOffset? RaisedTo { get; init; }
}
