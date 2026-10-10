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
}

public sealed record OrganizationNames(IReadOnlyDictionary<Guid, BilingualLabel> Departments, IReadOnlyDictionary<Guid, BilingualLabel> ExternalEntities)
{
    public static OrganizationNames None { get; } = new(new Dictionary<Guid, BilingualLabel>(), new Dictionary<Guid, BilingualLabel>());
}
