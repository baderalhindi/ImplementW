using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// The names a screen shows in place of ids, and the departments and entities a form offers (the 24 Oct 2026 review, H1: "add a read-only,
/// names-only lookup endpoint for every signed-in user"). The records that carry the ids are each authorised by their own module; this tells
/// names, and nothing else. An internal user is told every name. An external user is told its own entity and its own entity's people, and
/// nothing of AHDA's people or structure (ADR-013's isolation; AUD-CC-18 keeps AHDA's internal actors unnamed to an entity). An inactive
/// caller is told nothing.
/// </summary>
internal sealed class DisplayNameService(
    IAuthorizationEngine engine, IUserAdministrationRepository users, IDepartmentRepository departments, IExternalEntityRepository entities)
    : IDisplayNameService
{
    public async Task<DisplayNames> ListAsync(Guid callerId, DisplayNameQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (await CallerAsync(callerId, cancellationToken).ConfigureAwait(false) is not { } caller)
        {
            return new DisplayNames([]);
        }

        IReadOnlyList<UserName> people = await users.ListNamesAsync(query.UserIds, cancellationToken).ConfigureAwait(false);
        return new DisplayNames([.. query.UserIds.Distinct()
            .Select(id => people.FirstOrDefault(p => p.Id == id))
            .OfType<UserName>()
            .Where(p => caller.UserType == UserType.Internal || (p.ExternalEntityId is not null && p.ExternalEntityId == caller.ExternalEntityId))
            .Select(p => new PersonName(p.Id, p.DisplayName))]);
    }

    public async Task<OrganizationUnits> ListUnitsAsync(Guid callerId, CancellationToken cancellationToken)
    {
        if (await CallerAsync(callerId, cancellationToken).ConfigureAwait(false) is not { } caller)
        {
            return new OrganizationUnits([], []);
        }

        IReadOnlyList<OrganizationUnit> allEntities = await entities.ListUnitsAsync(cancellationToken).ConfigureAwait(false);
        return caller.UserType == UserType.Internal
            ? new OrganizationUnits(await departments.ListUnitsAsync(cancellationToken).ConfigureAwait(false), allEntities)
            : new OrganizationUnits([], [.. allEntities.Where(e => e.Id == caller.ExternalEntityId)]);
    }

    private async Task<AuthorizationPrincipal?> CallerAsync(Guid callerId, CancellationToken cancellationToken) =>
        await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is { IsActive: true } caller ? caller : null;
}
