using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// Finding a person for a field that names one (review round 5, item 5). <c>USER_VIEW</c> is the administrators': this tells a person who may
/// write something only who a field can name, by name, role and unit, and no contact details.
/// </summary>
public interface IPersonLookupService
{
    /// <summary>Null when the caller holds no write permission, so has no field to fill: a read-only role does not browse the directory.</summary>
    public Task<PersonMatchPage?> SearchAsync(Guid callerId, PersonLookupQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// The field's eligibility: the text matches the name, username or e-mail. <see cref="Scope"/> names the anchors a project role must cover,
/// as a task or risk owner must hold one. An external caller is held to its own entity whatever is asked.
/// </summary>
public sealed record PersonLookupQuery(string? Text, UserType? UserType, Guid? ExternalEntityId, RoleHolderScope? Scope, PageRequest Page);

public sealed record PersonMatch(
    Guid Id, string DisplayName, string Username, UserType UserType, Guid? DepartmentId, Guid? ExternalEntityId, IReadOnlyList<BilingualLabel> Roles, BilingualLabel? Unit);

public sealed record PersonMatchPage(IReadOnlyList<PersonMatch> Items, int Page, int PageSize, int TotalCount);
