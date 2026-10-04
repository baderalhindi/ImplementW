using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Application.Features.Progress;

/// <summary>Edge 13: the project's reporting periods, as stored.</summary>
internal sealed class ReportingCycleReader(IProgressRepository repository) : IReportingCycleReader
{
    public async Task<IReadOnlyList<ReportingCycleSummary>> ListAsync(Guid projectId, CancellationToken cancellationToken) =>
        [.. (await repository.ListCyclesAsync(projectId, cancellationToken).ConfigureAwait(false)).Select(ProgressMapping.ToSummary)];
}
