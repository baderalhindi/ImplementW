using PMPlatform.Application.Features.Project.Contracts.Events;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// A module that writes its own fact from a legacy intake (ADR-014; §8.2 edge 35). Project's one consumer of
/// <see cref="ProjectIntakeRecorded"/> calls every handler in the dispatch's one transaction, so the intake's facts commit together or are
/// all tried again. A handler is idempotent: a redelivered intake whose fact it already wrote changes nothing.
/// </summary>
public interface IProjectIntakeHandler
{
    /// <summary>The order handlers run in: a fact that reads another's runs after it (FinancialKpi's opening spend sits in Progress's period).</summary>
    public int Order { get; }

    public Task HandleAsync(ProjectIntakeRecorded intake, CancellationToken cancellationToken);
}
