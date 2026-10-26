namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// The canonical roles R01–R08 as another module may know them (E-U1): FG-04's approval authority and notification
/// recipient matrices name roles (ADR-004, OQ-005).
/// </summary>
public interface IRoleDirectory
{
    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken);
}
