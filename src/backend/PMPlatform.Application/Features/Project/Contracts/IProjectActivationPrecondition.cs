using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// A condition a module downstream of Project sets on the Planned → Active command, checked by that command before it writes.
/// Project calls every registered implementation and references none of them: the module that owns the fact implements this
/// contract, so the dependency stays that module → Project (ADR-003 §8.2), as an outcome handler's does to Approval. WF-03
/// implements it for ADR-009's gate — no ACTIVE baseline, no planned percentage, no activation (schedule-baseline.md D-11).
/// </summary>
public interface IProjectActivationPrecondition
{
    /// <summary>Null when met; otherwise the refusal the command answers with.</summary>
    public Task<AdministrationError?> CheckAsync(ProjectFacts project, CancellationToken cancellationToken);
}
