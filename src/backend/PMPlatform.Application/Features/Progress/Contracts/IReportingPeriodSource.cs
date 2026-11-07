using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>ADR-003 §8.2 edge 13: lets WF-14 report on a period WF-02 has not been reported on yet. It authorizes no one.</summary>
public interface IReportingPeriodSource
{
    /// <summary>The project's periods, generating those due in the caller's unit of work (M-11); earliest first.</summary>
    public Task<IReadOnlyList<ReportingCycleSummary>> StageDueAsync(Guid callerId, ProjectFacts project, CancellationToken cancellationToken);
}
