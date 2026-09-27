using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>The <c>identity_access.department</c> rows as ADM-011/012 read and write them.</summary>
public interface IDepartmentRepository
{
    public Task<DepartmentPage> ListAsync(DepartmentQuery query, CancellationToken cancellationToken);

    public Task<Versioned<DepartmentDetail>?> FindDetailAsync(Guid departmentId, CancellationToken cancellationToken);

    public Task<Department?> FindForUpdateAsync(Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Every department's parent: the whole tree, a small set (indexing-strategy P-8).</summary>
    public Task<IReadOnlyDictionary<Guid, Guid?>> GetParentsAsync(CancellationToken cancellationToken);

    /// <summary>Whether an active department other than <paramref name="exceptDepartmentId"/> has this directory reference.</summary>
    public Task<bool> IsDirectoryReferenceInUseAsync(string directoryReference, Guid? exceptDepartmentId, CancellationToken cancellationToken);

    public void Add(Department department);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
