using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// ADR-009's gate on Project's Planned → Active command: without an ACTIVE baseline there is no planned percentage, so the
/// project does not go Active. An APPROVED baseline meets it — approved through WF-11, or activated on submission under a
/// profile that requires no approval (ADR-015) — and so does a Declared Baseline for a legacy-intake project (ADR-014).
/// WF-03 reports the readiness and Project owns the transition (the spec's DCL-SCH-22).
/// </summary>
internal sealed class BaselineActivationPrecondition(IScheduleRepository repository) : IProjectActivationPrecondition
{
    public async Task<AdministrationError?> CheckAsync(ProjectFacts project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return await repository.FindActiveBaselineAsync(project.Id, track: false, cancellationToken).ConfigureAwait(false) switch
        {
            { BaselineType: BaselineType.Approved } => null,
            { BaselineType: BaselineType.Declared } when project.LegacyIntakeDate is not null => null,
            _ => AdministrationError.Rule(ScheduleErrorCodes.ActiveBaselineRequired),
        };
    }
}
