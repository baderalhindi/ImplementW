namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// Review round 7, item 2: a module's open records of a project that its Project Manager holds, moved to the new manager when the manager changes. Each
/// module implements it over its own records (it already references Project's contracts), and the change calls every one in its unit of work, so the
/// project and its open work change hands together or not at all.
/// </summary>
public interface IProjectManagerHandover
{
    /// <summary>What it moves, as the audit names it: <c>tasks</c>, <c>update_requests</c>.</summary>
    public string Records { get; }

    /// <summary>Moves the project's open records held by <paramref name="fromUserId"/> to <paramref name="toUserId"/>; how many moved.</summary>
    public Task<int> HandOverAsync(Guid projectId, Guid fromUserId, Guid toUserId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Assign or change a project's Project Manager, and why.</summary>
public sealed record ProjectManagerChange(Guid ProjectManagerUserId, Domain.Common.NarrativeText Reason);
