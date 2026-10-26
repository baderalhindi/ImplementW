using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>The people a screen names by id (the 24 Oct 2026 review, H1). At most <see cref="MaxIds"/>.</summary>
public sealed record DisplayNameQuery(IReadOnlyCollection<Guid> UserIds)
{
    public const int MaxIds = 100;
}

/// <summary>The names the caller may be told, in the order of the ids asked for; an id that names nobody, or nobody the caller may know, is left out.</summary>
public sealed record DisplayNames(IReadOnlyList<PersonName> Users);

public sealed record PersonName(Guid Id, string DisplayName);

/// <summary>A user's display name and the entity an external user belongs to.</summary>
public sealed record UserName(Guid Id, string DisplayName, Guid? ExternalEntityId);

/// <summary>A department or an external entity as a screen names and offers it: its code, its bilingual name and whether it is active.</summary>
public sealed record OrganizationUnit(Guid Id, string Code, BilingualLabel Name, bool IsActive);

/// <summary>The departments and external entities the caller may be told of, by code.</summary>
public sealed record OrganizationUnits(IReadOnlyList<OrganizationUnit> Departments, IReadOnlyList<OrganizationUnit> ExternalEntities);

/// <summary>
/// Names only, for every signed-in user (review H1): no record, attribute or state of a person, and of a department or entity its code, name
/// and whether it is active — what a screen names and a form offers.
/// </summary>
public interface IDisplayNameService
{
    public Task<DisplayNames> ListAsync(Guid callerId, DisplayNameQuery query, CancellationToken cancellationToken);

    public Task<OrganizationUnits> ListUnitsAsync(Guid callerId, CancellationToken cancellationToken);
}
