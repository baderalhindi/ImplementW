namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 44, Closure → ChangeRequest (query; TASK-063): what WF-10's readiness needs of a project's change requests. WF-10
/// never withdraws, closes or implements one (BR-CLO-015, BR-CLO-025). It authorizes no one.
/// </summary>
public interface IChangeRequestCloseoutReader
{
    public Task<ChangeRequestCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <param name="Undecided">Requests DRAFT, SUBMITTED, UNDER_REVIEW or RETURNED: not yet decided.</param>
/// <param name="ApprovedNotStarted">Requests APPROVED whose implementation has not started.</param>
/// <param name="InImplementation">Requests in IMPLEMENTATION, or IMPLEMENTED and not yet CLOSED.</param>
public sealed record ChangeRequestCloseoutPosition(int Undecided, int ApprovedNotStarted, int InImplementation);
