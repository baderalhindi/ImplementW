namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADR-013: per-project access ends on project closure. Project closure (TASK-063) calls this inside its own authorised
/// command; IdentityAccess owns the assignments, so it ends them (M-1).
/// </summary>
public interface IProjectAccessLifecycle
{
    /// <summary>Ends every active assignment on <paramref name="projectId"/> as PROJECT_CLOSED; returns how many.</summary>
    public Task<int> EndAccessForClosedProjectAsync(Guid projectId, Guid actorUserId, CancellationToken cancellationToken);
}
