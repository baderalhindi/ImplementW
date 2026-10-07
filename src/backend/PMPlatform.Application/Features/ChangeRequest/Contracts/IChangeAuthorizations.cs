using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// ADR-003 §8.2 edges 11 and 12: the typed adapter through which a target module presents a WF-08 change authorisation — WF-03 before
/// and as a rebaseline takes effect, WF-14 before and as a change to the Approved Budget does. Approval of a change request only issues
/// authorisations; the change happens when its target module applies one here, in that module's own unit of work, as an explicit and
/// separately audited step. It authorizes no person: the target module has already decided its caller may make the change.
/// </summary>
public interface IChangeAuthorizations
{
    /// <summary>
    /// Whether the authorisation would apply to the claim now: <see cref="ChangeAuthorizationVerdict.Applicable"/>, or why not. Reads only,
    /// for a target module accepting a change that takes effect later (a candidate submitted to its own approval).
    /// </summary>
    public Task<ChangeAuthorizationVerdict> CheckAsync(ChangeAuthorizationClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the authorisation, exactly once: ISSUED → APPLIED with the use's actor, time and reference, and the audit event
    /// <c>ChangeRequest.ChangeAuthorizationApplied</c>, staged in the caller's unit of work so they commit with the change or not at all
    /// (M-11). The same use again — the same reference — is <see cref="ChangeAuthorizationVerdict.Replayed"/> and changes nothing; any
    /// other use of an applied authorisation is <see cref="ChangeAuthorizationVerdict.Consumed"/>. The authorisation's row version refuses
    /// a concurrent second application when the caller saves.
    /// </summary>
    public Task<ChangeAuthorizationVerdict> ApplyAsync(ChangeAuthorizationClaim claim, ChangeAuthorizationUse use, CancellationToken cancellationToken);
}

/// <summary>
/// What the target module asserts about the change it makes: the authorisation, the project, the kind of change, and the target as it
/// stands — the identifier and version of the commitment the change replaces.
/// </summary>
public sealed record ChangeAuthorizationClaim(Guid ChangeAuthorizationId, Guid ProjectId, ChangeAuthorizationScope Scope, Guid TargetId, int TargetRevisionNo);

/// <summary>
/// One application: who made the change, when, and the target module's record that carries it, named by <see cref="ReferenceOf"/>.
/// The reference is the application's idempotency key, derived from the target module's own aggregate (api-conventions R-39).
/// </summary>
public sealed record ChangeAuthorizationUse(Guid ActorUserId, string AppliedReference, DateTimeOffset AppliedAt)
{
    /// <summary>A record's reference: <c>{module}.{type}:{id}</c>, e.g. <c>Schedule.ProjectBaseline:…</c>.</summary>
    public static string ReferenceOf(string module, string type, Guid id) => $"{module}.{type}:{id}";
}

/// <summary>The answer to a claim.</summary>
public enum ChangeAuthorizationVerdict
{
    /// <summary>It would apply now.</summary>
    Applicable = 1,

    /// <summary>It was applied by this call.</summary>
    Applied = 2,

    /// <summary>It was applied earlier by the same use; nothing changed.</summary>
    Replayed = 3,

    /// <summary>No such authorisation.</summary>
    NotFound = 4,

    /// <summary>It is of another project, or permits another kind of change.</summary>
    OutOfScope = 5,

    /// <summary>The target it pins is no longer the target as it stands: the commitment changed since approval.</summary>
    TargetMoved = 6,

    /// <summary>Its change request is not in IMPLEMENTATION: AHDA has not started implementing it, or has finished.</summary>
    NotImplementing = 7,

    /// <summary>It was applied already, by another use.</summary>
    Consumed = 8,

    /// <summary>It expired or was revoked.</summary>
    Ended = 9,
}

/// <summary>The targets WF-08 authorises changes to, as their modules name them.</summary>
public static class ChangeTargets
{
    public const string ScheduleModule = "Schedule";
    public const string ProjectBaseline = "ProjectBaseline";
    public const string FinancialKpiModule = "FinancialKpi";
    public const string FinancialCommitment = "FinancialCommitment";
}
