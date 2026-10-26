using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>
/// WF-13 §7 (TASK-066): an accepted revision applied to its source record through its allowlisted typed adapter — never a generic field
/// write. Each attempt revalidates the source against the version the revision expects: a changed source is a CONFLICT that applies nothing
/// until an AHDA user revalidates it; the source's own rules may refuse it (FAILED); otherwise the change and the attempt commit together.
/// An <c>Idempotency-Key</c> repeated finds its attempt, and a revision is applied at most once. AHDA's only: internal users.
/// </summary>
public interface ISourceApplicationService
{
    /// <summary>The revision's attempts, newest first; empty for an external caller or a revision the caller may not see.</summary>
    public Task<SourceApplicationPage> ListAsync(Guid callerId, Guid contributionId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<SourceApplicationDetail>>> GetAsync(Guid callerId, Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Attempts to apply an ACCEPTED_PENDING_APPLICATION revision. EXTERNAL_CONTRIBUTION_APPLY, internal users.</summary>
    public Task<AdministrationResult<SourceApplicationOutcome>> ApplyAsync(Guid callerId, Guid contributionId, Guid requestKey, CancellationToken cancellationToken);

    /// <summary>Confirms, on the latest CONFLICT attempt, that the accepted values apply to the source record as it now is; the next attempt expects it.</summary>
    public Task<AdministrationResult<Versioned<SourceApplicationDetail>>> RevalidateAsync(Guid callerId, Guid applicationId, uint? expectedVersion, CancellationToken cancellationToken);
}
