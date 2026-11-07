using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <inheritdoc cref="IPersonLookupService"/>
internal sealed class PersonLookupService(
    IAuthorizationEngine engine,
    IUserAdministrationRepository users,
    IRoleDirectory roles,
    IRoleHolderDirectory holders,
    IDepartmentRepository departments,
    IExternalEntityRepository entities) : IPersonLookupService
{
    /// <summary>Enough for a typed name to find its person among the holders of a project role, which are filtered after the text.</summary>
    private const int CandidateLimit = 200;

    public async Task<PersonMatchPage?> SearchAsync(Guid callerId, PersonLookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is not { IsActive: true } caller
            || !caller.Grants.Any(g => PermissionCatalogue.Platform.Contains(g.PermissionCode) && PermissionCatalogue.Platform.Get(g.PermissionCode).Mode == AccessMode.Write))
        {
            return null;
        }

        bool external = caller.UserType != UserType.Internal;
        UserPage candidates = await users.ListAsync(
            new UserQuery(
                [UserStatus.Active],
                external ? [UserType.External] : query.UserType is { } type ? [type] : [],
                null,
                external ? caller.ExternalEntityId : query.ExternalEntityId,
                query.Text,
                UserSort.DisplayNameAscending,
                new PageRequest(1, CandidateLimit)),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<RoleSummary> allRoles = await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<UserSummary> eligible = candidates.Items;
        if (query.Scope is { } scope)
        {
            HashSet<Guid> holding = [.. (await holders.FindHoldersAsync([.. allRoles.Select(r => r.Id)], scope, cancellationToken).ConfigureAwait(false)).Select(h => h.UserId)];
            eligible = eligible.Where(u => holding.Contains(u.Id));
        }

        List<UserSummary> found = [.. eligible];
        Dictionary<string, BilingualLabel> roleNames = allRoles.ToDictionary(r => r.Code, r => r.Name, StringComparer.Ordinal);
        IReadOnlyList<OrganizationUnit> departmentUnits = await departments.ListUnitsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<OrganizationUnit> entityUnits = await entities.ListUnitsAsync(cancellationToken).ConfigureAwait(false);

        List<PersonMatch> matches = [];
        foreach (UserSummary user in found.Skip(query.Page.Skip).Take(query.Page.PageSize))
        {
            AuthorizationPrincipal? principal = await engine.GetPrincipalAsync(user.Id, cancellationToken).ConfigureAwait(false);
            matches.Add(new PersonMatch(
                user.Id,
                user.DisplayName,
                user.Username,
                user.UserType,
                user.DepartmentId,
                user.ExternalEntityId,
                [.. (principal?.RoleCodes ?? []).Where(roleNames.ContainsKey).Select(code => roleNames[code])],
                departmentUnits.FirstOrDefault(d => d.Id == user.DepartmentId)?.Name ?? entityUnits.FirstOrDefault(e => e.Id == user.ExternalEntityId)?.Name));
        }

        return new PersonMatchPage(matches, query.Page.Page, query.Page.PageSize, found.Count);
    }
}
