using System.Collections.Concurrent;
using PMPlatform.Application.Features.Progress;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>Stands in for WF-03, WF-04 and WF-14 (progress-update.md F-1): a test sets what each project's sources say.</summary>
internal sealed class FakeProgressInputs : IProgressInputs
{
    private readonly ConcurrentDictionary<Guid, ProgressInputs> _inputs = new();

    public Task<ProgressInputs> ReadAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(_inputs.GetValueOrDefault(projectId, ProgressInputs.None));

    /// <summary>
    /// One leaf task of 100 days at <paramref name="actualPercent"/>, and a one-activity baseline from
    /// <paramref name="baselineStart"/> of 100 days, so planned is the days elapsed; schedule and finance as given.
    /// </summary>
    public void Set(Guid projectId, decimal actualPercent, DateOnly? baselineStart, HealthStatus? schedule = HealthStatus.Green, HealthStatus? financial = HealthStatus.Green) =>
        _inputs[projectId] = new ProgressInputs(
            [new WorkItemProgress(Guid.NewGuid(), null, 100, actualPercent)],
            baselineStart is { } start ? new BaselinePlan(BaselineId, [new BaselineActivity(Guid.NewGuid(), start, start.AddDays(99))]) : null,
            schedule,
            financial);

    public void Clear(Guid projectId) => _inputs.TryRemove(projectId, out _);

    public static readonly Guid BaselineId = new("00000000-0440-4000-8000-0000000000b1");
}
