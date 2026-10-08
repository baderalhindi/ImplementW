namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 46, Closure → Progress (query; TASK-063): what WF-10's readiness needs of a project's progress reporting. WF-10 never
/// publishes progress or coerces it to 100% (WF-10 §8.1). It authorizes no one.
/// </summary>
public interface IProgressCloseoutReader
{
    public Task<ProgressCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <param name="Unpublished">Progress submissions DRAFT, SUBMITTED, UNDER_REVIEW or RETURNED.</param>
/// <param name="HasPublished">Whether any submission of the project has been PUBLISHED: its final reporting state.</param>
public sealed record ProgressCloseoutPosition(int Unpublished, bool HasPublished);
