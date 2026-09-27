using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

internal sealed class FakeDepartmentRepository : FakeStore<Department>, IDepartmentRepository
{
    public Task<DepartmentPage> ListAsync(DepartmentQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(PageOf(
            Rows.Values.OrderBy(d => d.Code, StringComparer.Ordinal).Select(d => new DepartmentSummary(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.IsActive)),
            query.Page,
            (items, total) => new DepartmentPage(items, query.Page.Page, query.Page.PageSize, total)));

    public Task<Versioned<DepartmentDetail>?> FindDetailAsync(Guid departmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.TryGetValue(departmentId, out Department? d)
            ? new Versioned<DepartmentDetail>(
                new DepartmentDetail(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.DirectoryReference, d.IsActive, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy),
                Versions[d.Id])
            : null);

    public Task<Department?> FindForUpdateAsync(Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken) =>
        Task.FromResult(Track(departmentId, expectedVersion));

    public Task<IReadOnlyDictionary<Guid, Guid?>> GetParentsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Guid?>>(Rows.Values.ToDictionary(d => d.Id, d => d.ParentDepartmentId));

    public Task<bool> IsDirectoryReferenceInUseAsync(string directoryReference, Guid? exceptDepartmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.Values.Any(d => d.DirectoryReference == directoryReference && d.IsActive && d.Id != exceptDepartmentId));
}
