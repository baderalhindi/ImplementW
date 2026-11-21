namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-011 Organization Structure and ADM-012 Departments (TASK-031). A department is never deleted (RETAIN); it is
/// deactivated, and the users, projects and assignments that reference it keep the reference.
/// </summary>
public interface IDepartmentAdministrationService
{
    public Task<AdministrationResult<DepartmentPage>> ListAsync(Guid actorId, DepartmentQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<DepartmentDetail>>> GetAsync(Guid actorId, Guid departmentId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<DepartmentDetail>>> CreateAsync(Guid actorId, DepartmentDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<DepartmentDetail>>> UpdateAsync(
        Guid actorId, Guid departmentId, DepartmentChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<DepartmentDetail>>> ActivateAsync(Guid actorId, Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Client review, item 6: the department leaves administration and every picker; records keep its name. Refused (409
    /// IDENTITY_ACCESS_DEPARTMENT_IN_USE, a field per kind) while users, sub-departments, open projects or assignments still depend on it. Null on success.
    /// </summary>
    public Task<AdministrationError?> DeleteAsync(Guid actorId, Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<DepartmentDetail>>> DeactivateAsync(Guid actorId, Guid departmentId, uint? expectedVersion, CancellationToken cancellationToken);
}
