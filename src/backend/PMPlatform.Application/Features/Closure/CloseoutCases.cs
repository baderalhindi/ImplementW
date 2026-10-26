using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>What the two case registers share: when a case's fields change, and the deletion of a draft.</summary>
internal static class CloseoutCases
{
    /// <summary>A REJECTED, WITHDRAWN or EFFECTED case changes no more; one past its requester keeps the fields it was submitted with.</summary>
    public static AdministrationError? EditRefused(CloseoutCase @case) =>
        CloseoutWorkflow.IsFinal(@case.Status) ? AdministrationError.TerminalState
        : CloseoutWorkflow.IsEditable(@case.Status) ? null
        : AdministrationError.Conflict(ClosureErrorCodes.CaseNotEditable);

    /// <summary>
    /// HARD_DRAFT, and only a draft never submitted: a submitted case is history, withdrawn rather than removed, and so is a draft that
    /// has readiness records or obligations (409 CLOSURE_CASE_IN_USE). Null when deleted, or when there is nothing the caller may see.
    /// </summary>
    public static async Task<AdministrationError?> DeleteAsync<TCase>(
        ICloseoutRepository repository, CloseoutGate gate, IAuditTrail audit, Guid callerId, Guid caseId, uint? expectedVersion, CancellationToken cancellationToken)
        where TCase : CloseoutCase
    {
        Loaded<TCase> loaded = await gate.LoadCaseAsync<TCase>(callerId, PermissionCatalogue.CloseoutRaise, caseId, expectedVersion, false, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        TCase @case = loaded.Record!;
        if (@case.Status != CloseoutCaseStatus.Draft)
        {
            return CloseoutWorkflow.IsFinal(@case.Status) ? AdministrationError.TerminalState : AdministrationError.Conflict(ClosureErrorCodes.CaseNotEditable);
        }

        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(CloseoutAudit.Deleted(callerId, loaded.Project!, @case));
        repository.Remove(@case);
        CloseoutSaveOutcome saved = await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        return saved == CloseoutSaveOutcome.Saved ? null : CloseoutGate.RefusalOf(saved);
    }
}
