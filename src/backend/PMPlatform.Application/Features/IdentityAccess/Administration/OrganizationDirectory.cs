using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>Organization reference data another module names, read without a caller (TASK-041).</summary>
internal sealed class OrganizationDirectory(IDepartmentRepository departments, IExternalEntityRepository entities) : IOrganizationDirectory
{
    public async Task<bool> IsActiveDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) =>
        (await departments.FindDetailAsync(departmentId, cancellationToken).ConfigureAwait(false))?.Value.IsActive == true;

    public async Task<bool> IsActiveExternalEntityAsync(Guid externalEntityId, CancellationToken cancellationToken) =>
        (await entities.FindDetailAsync(externalEntityId, cancellationToken).ConfigureAwait(false))?.Value.Status == ExternalEntityStatus.Active;
}
