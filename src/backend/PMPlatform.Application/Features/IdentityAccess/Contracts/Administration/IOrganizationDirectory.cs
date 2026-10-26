using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// Whether a department or an external entity may be named by a new or changed record now (E-U1), without the caller
/// needing ADM-011–013's administration permissions: a project names its owning department and delivering entity (TASK-041).
/// </summary>
public interface IOrganizationDirectory
{
    /// <summary>True only for an existing department that is active.</summary>
    public Task<bool> IsActiveDepartmentAsync(Guid departmentId, CancellationToken cancellationToken);

    /// <summary>True only for an existing entity that is ACTIVE.</summary>
    public Task<bool> IsActiveExternalEntityAsync(Guid externalEntityId, CancellationToken cancellationToken);

    /// <summary>
    /// The names of the departments and entities named, in Arabic and English, whatever their state: a report names the department and the
    /// delivering entity of each project it lists (TASK-071). An id that names nothing is left out.
    /// </summary>
    public Task<OrganizationNames> ListNamesAsync(IReadOnlyCollection<Guid> departmentIds, IReadOnlyCollection<Guid> externalEntityIds, CancellationToken cancellationToken);

    /// <summary>The department with this code, whatever its state; a migration workbook names a department by code (TASK-102).</summary>
    public Task<Guid?> FindDepartmentIdAsync(string code, CancellationToken cancellationToken);

    /// <summary>The external entity with this code, whatever its state (TASK-102).</summary>
    public Task<Guid?> FindExternalEntityIdAsync(string code, CancellationToken cancellationToken);

    /// <summary>Every active department's code and name, by code: the values a migration template lists (TASK-102).</summary>
    public Task<IReadOnlyList<OrganizationCode>> ListActiveDepartmentCodesAsync(CancellationToken cancellationToken);

    /// <summary>Every ACTIVE external entity's code and name, by code (TASK-102).</summary>
    public Task<IReadOnlyList<OrganizationCode>> ListActiveExternalEntityCodesAsync(CancellationToken cancellationToken);
}

public sealed record OrganizationCode(string Code, BilingualLabel Name);

public sealed record OrganizationNames(IReadOnlyDictionary<Guid, BilingualLabel> Departments, IReadOnlyDictionary<Guid, BilingualLabel> ExternalEntities)
{
    public static OrganizationNames None { get; } = new(new Dictionary<Guid, BilingualLabel>(), new Dictionary<Guid, BilingualLabel>());
}
