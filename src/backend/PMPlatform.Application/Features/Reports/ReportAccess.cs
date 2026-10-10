using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>A report version's columns, and its parameters each with its options, in their order.</summary>
internal sealed record ReportParts(IReadOnlyList<ReportColumn> Columns, IReadOnlyList<(ReportParameter Parameter, IReadOnlyList<ReportParameterOption> Options)> Parameters);

/// <summary>
/// Who may run what (FG-02 §8.1): a caller's roles select the PUBLISHED reports whose audience they are in — an external entity's person only the
/// entity report set (ADR-013) — and select nothing else; the data is authorised cell by cell. Whether a caller holds a permission at all — the
/// export permission, the explorer's — is the authorization engine's answer, read now, never the token's.
/// </summary>
internal sealed class ReportAccess(IReportRepository repository, IUserRoleDirectory userRoles, IRoleDirectory roles, IAuthorizationEngine engine)
{
    public Task<UserRoles> CallerAsync(Guid callerId, CancellationToken cancellationToken) => userRoles.FindAsync(callerId, cancellationToken);

    /// <summary>The report's PUBLISHED version, if the caller may run it; otherwise null, as if it did not exist (R-47).</summary>
    public async Task<ReportDefinition?> OpenAsync(Guid callerId, ReportCode code, CancellationToken cancellationToken) =>
        await repository.FindPublishedAsync(code, track: false, cancellationToken).ConfigureAwait(false) is { } definition
        && await MayRunAsync(callerId, definition, cancellationToken).ConfigureAwait(false)
            ? definition
            : null;

    /// <summary>Whether the caller's roles are the version's audience and ADR-013 lets them run it.</summary>
    public async Task<bool> MayRunAsync(Guid callerId, ReportDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        UserRoles caller = await CallerAsync(callerId, cancellationToken).ConfigureAwait(false);
        if (!ReportCatalogue.MayRun(definition.Code, caller.IsExternal))
        {
            return false;
        }

        IReadOnlyDictionary<Guid, string> codes = await RoleCodesAsync(cancellationToken).ConfigureAwait(false);
        return (await repository.ListAudienceAsync([definition.Id], track: false, cancellationToken).ConfigureAwait(false))
            .Any(a => caller.RoleCodes.Contains(codes[a.RoleId]));
    }

    /// <summary>Whether the caller holds <paramref name="permissionCode"/> at some scope now. A question, so a negative answer records nothing.</summary>
    public async Task<bool> HoldsAsync(Guid callerId, string permissionCode, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(permissionCode), cancellationToken).ConfigureAwait(false)).IsAllowed;

    /// <summary>Whether the caller's account is active now: a job runs for its requester only while they may still sign in.</summary>
    public async Task<bool> IsActiveAsync(Guid callerId, CancellationToken cancellationToken) =>
        (await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false))?.IsActive == true;

    public async Task<ReportParts> PartsAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReportColumn> columns = await repository.ListColumnsAsync(definitionId, track: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReportParameter> parameters = await repository.ListParametersAsync(definitionId, track: false, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, ReportParameterOption> options = (await repository.ListOptionsAsync([.. parameters.Select(p => p.Id)], track: false, cancellationToken).ConfigureAwait(false))
            .ToLookup(o => o.ReportParameterId);
        return new ReportParts(columns, [.. parameters.Select(p => (p, (IReadOnlyList<ReportParameterOption>)[.. options[p.Id]]))]);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> RoleCodesAsync(CancellationToken cancellationToken) =>
        (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id, r => r.Code);
}
