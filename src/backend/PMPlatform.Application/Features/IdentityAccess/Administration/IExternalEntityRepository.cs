using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>The <c>identity_access.external_entity</c> rows as ADM-013 reads and writes them.</summary>
public interface IExternalEntityRepository
{
    public Task<ExternalEntityPage> ListAsync(ExternalEntityQuery query, CancellationToken cancellationToken);

    public Task<Versioned<ExternalEntityDetail>?> FindDetailAsync(Guid entityId, CancellationToken cancellationToken);

    public Task<ExternalEntity?> FindForUpdateAsync(Guid entityId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Whether the master data item belongs to catalogue EXTERNAL_ENTITY_TYPE.</summary>
    public Task<bool> IsEntityTypeAsync(Guid itemId, CancellationToken cancellationToken);

    public void Add(ExternalEntity entity);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
