using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// The activation of approved cases (WF-10 §5 C07, C12; Completion.Activate and Closure.Activate are "System/service"): each APPROVED case is
/// activated as WF-10's service principal, its eligibility and readiness read again at that moment (BR-CLO-034). Approval and activation stay
/// two transactions with two audit events (P3). One case per unit of work, so one refusal touches no other: a refused case — a blocker that
/// appeared after its approval — stays APPROVED and is tried again on the next pass, and its staged rows are discarded. A case whose
/// activation fails — the database refusing its commit, or briefly unavailable — is rolled back, logged and tried again on a later pass,
/// without holding up the cases after it. Two instances activating the same case cannot both commit: each changes the case's and the
/// project's rows.
/// </summary>
internal sealed partial class CloseoutMaintenance(
    ICloseoutRepository repository, IProjectFactsReader projects, CloseoutActivation activation, TimeProvider timeProvider, ILogger<CloseoutMaintenance> logger)
    : ICloseoutMaintenance
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        int effected = 0;
        foreach (Guid due in await repository.ListApprovedAsync<CompletionCase>(batchSize, cancellationToken).ConfigureAwait(false))
        {
            effected += await TryActivateAsync<CompletionCase>(due, cancellationToken).ConfigureAwait(false) ? 1 : 0;
        }

        foreach (Guid due in await repository.ListApprovedAsync<ClosureCase>(batchSize, cancellationToken).ConfigureAwait(false))
        {
            effected += await TryActivateAsync<ClosureCase>(due, cancellationToken).ConfigureAwait(false) ? 1 : 0;
        }

        return effected;
    }

    private async Task<bool> TryActivateAsync<TCase>(Guid caseId, CancellationToken cancellationToken)
        where TCase : CloseoutCase
    {
        try
        {
            return await ActivateAsync<TCase>(caseId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            repository.Discard();
            LogNotActivated(logger, caseId, exception.GetType().Name);
            return false;
        }
    }

    private async Task<bool> ActivateAsync<TCase>(Guid caseId, CancellationToken cancellationToken)
        where TCase : CloseoutCase
    {
        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        TCase? @case = await repository.FindCaseAsync<TCase>(caseId, null, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = @case is null ? null : await projects.FindAsync(@case.ProjectId, cancellationToken).ConfigureAwait(false);
        bool effected = @case?.Status == CloseoutCaseStatus.Approved && project is not null
                        && await activation.ActivateAsync(work, @case, project, CloseoutServicePrincipal.Id, AuditActorType.Service, timeProvider.GetUtcNow(), cancellationToken)
                            .ConfigureAwait(false) is null;
        if (!effected)
        {
            repository.Discard();
        }

        return effected;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Closeout case {CaseId} was not activated: {Reason}. It is tried again on a later pass.")]
    private static partial void LogNotActivated(ILogger logger, Guid caseId, string reason);
}
