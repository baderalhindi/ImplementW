using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>ADM-011/012 over <c>identity_access.department</c> (TASK-031).</summary>
internal sealed class DepartmentRepository(PMPlatformDbContext context) : IDepartmentRepository
{
    public async Task<DepartmentPage> ListAsync(DepartmentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Department> departments = context.Set<Department>().AsNoTracking();
        if (query.IsActive is { } isActive)
        {
            departments = departments.Where(d => d.IsActive == isActive);
        }

        if (query.ParentDepartmentId is { } parentId)
        {
            departments = departments.Where(d => d.ParentDepartmentId == parentId);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            string pattern = AdministrationPersistence.ContainsPattern(query.Text.Trim());
            departments = departments.Where(d => EF.Functions.ILike(d.Code, pattern) || EF.Functions.ILike(d.Name.Ar, pattern) || EF.Functions.ILike(d.Name.En, pattern));
        }

        int totalCount = await departments.CountAsync(cancellationToken).ConfigureAwait(false);
        List<DepartmentSummary> items = await departments
            .OrderBy(d => d.Code).ThenBy(d => d.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(d => new DepartmentSummary(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.IsActive))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new DepartmentPage(items, query.Page.Page, query.Page.PageSize, totalCount);
    }

    public async Task<Versioned<DepartmentDetail>?> FindDetailAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var row = await context.Set<Department>().AsNoTracking()
            .Where(d => d.Id == departmentId)
            .Select(d => new { Department = d, Version = EF.Property<uint>(d, EntityTypeBuilderExtensions.RowVersion) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        Department department = row.Department;
        return new Versioned<DepartmentDetail>(
            new DepartmentDetail(
                department.Id, department.Code, department.Name, department.ParentDepartmentId, department.DirectoryReference, department.IsActive,
                department.CreatedAt, department.CreatedBy, department.UpdatedAt, department.UpdatedBy),
            row.Version);
    }

    public async Task<Department?> FindForUpdateAsync(Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Department? department = await context.Set<Department>().SingleOrDefaultAsync(d => d.Id == departmentId, cancellationToken).ConfigureAwait(false);
        if (department is not null)
        {
            AdministrationPersistence.ExpectVersion(context, department, expectedVersion);
        }

        return department;
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetParentsAsync(CancellationToken cancellationToken) =>
        await context.Set<Department>().AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.ParentDepartmentId, cancellationToken).ConfigureAwait(false);

    public Task<bool> IsDirectoryReferenceInUseAsync(string directoryReference, Guid? exceptDepartmentId, CancellationToken cancellationToken) =>
        context.Set<Department>().AsNoTracking()
            .AnyAsync(d => d.DirectoryReference == directoryReference && d.IsActive && d.Id != exceptDepartmentId, cancellationToken);

    public void Add(Department department) => context.Set<Department>().Add(department);

    public async Task<IReadOnlyDictionary<Guid, BilingualLabel>> ListNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await context.Set<Department>().AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken).ConfigureAwait(false);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => AdministrationPersistence.SaveAsync(context, cancellationToken);
}
