using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// Until TASK-057 builds WF-07: there is no issue register, so no issue is raised (503, the dependency is not there) and no
/// risk has an issue. TASK-057 registers its own <see cref="IRiskIssueMaterialisation"/> in place of this one.
/// </summary>
internal sealed class UnbuiltIssueRegister : IRiskIssueMaterialisation
{
    public Task<AdministrationResult<OriginatedIssue>> RaiseIssueAsync(RiskIssueCommand command, CancellationToken cancellationToken) =>
        Task.FromResult<AdministrationResult<OriginatedIssue>>(AdministrationError.Unavailable);

    public Task<IReadOnlyList<OriginatedIssue>> ListByOriginatingRisksAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OriginatedIssue>>([]);
}
