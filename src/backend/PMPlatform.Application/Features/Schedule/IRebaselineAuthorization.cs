namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// Where WF-03 asks whether a rebaseline is authorised: every rebaseline after the first APPROVED baseline implements an
/// approved WF-08 change authorisation (BR-SCH-034, DCL-SCH-21), read through ADR-003 §8.2 edge 11. ChangeRequest is not
/// built (TASK-060), so the only implementation is <see cref="NoRebaselineAuthorization"/>, which knows none: a rebaseline
/// is refused until TASK-060 connects it (schedule-baseline.md F-1).
/// </summary>
internal interface IRebaselineAuthorization
{
    /// <summary>Whether the authorisation is approved, of the project, and not yet applied.</summary>
    public Task<bool> IsApplicableAsync(Guid projectId, Guid changeAuthorizationId, CancellationToken cancellationToken);
}

/// <summary>Until TASK-060: no change authorisation exists, so none is applicable.</summary>
internal sealed class NoRebaselineAuthorization : IRebaselineAuthorization
{
    public Task<bool> IsApplicableAsync(Guid projectId, Guid changeAuthorizationId, CancellationToken cancellationToken) => Task.FromResult(false);
}
