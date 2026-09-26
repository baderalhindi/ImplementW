using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>
/// The shipped-default grants against the platform catalogue: every role × every permission. A granted cell has an allow
/// and a deny case; a withheld cell is denied, 403 at the gate and 404 on a record. Only the source-backed rows are shipped (seed record F-1).
/// </summary>
public sealed class ShippedGrantTests
{
    public static TheoryData<string, string> GrantedCells()
    {
        TheoryData<string, string> data = [];
        foreach (ShippedGrant grant in PermissionCatalogue.ShippedDefaultGrants)
        {
            data.Add(grant.RoleCode, grant.PermissionCode);
        }

        return data;
    }

    public static TheoryData<string, string> WithheldCells()
    {
        TheoryData<string, string> data = [];
        foreach (string role in RoleCodes)
        {
            foreach (PermissionDefinition permission in PermissionCatalogue.Platform.Definitions.Where(p => ShippedGrant(role, p.Code) is null))
            {
                data.Add(role, permission.Code);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GrantedCells))]
    public async Task AGrantedCellAllowsItsHolder(string role, string permission)
    {
        AuthorizationScenario scenario = Scenario(role);

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(permission));
        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(permission, new AuthorizationSubject { OwnerUserId = UserId }));
    }

    [Theory]
    [MemberData(nameof(GrantedCells))]
    public async Task AGrantedCellDeniesOutsideItsScope(string role, string permission)
    {
        AuthorizationScenario scenario = Scenario(role);
        DataScope scope = ShippedGrant(role, permission)!.Scope;

        (AuthorizationDecision decision, AuthorizationDecision expected) = scope switch
        {
            // Another user's layout or report definition: not visible, so 404 (R-47).
            DataScope.Own => (await scenario.AuthorizeAsync(permission, new AuthorizationSubject { OwnerUserId = OtherUserId }),
                AuthorizationDecision.NotFound(AuthorizationDenial.OutOfScope)),
            // ALL leaves nothing out of scope; the same holder, disabled, holds nothing.
            DataScope.All => (await Disabled(scenario).AuthorizeAsync(permission), AuthorizationDecision.Forbidden(AuthorizationDenial.InactivePrincipal)),
            // No shipped grant has another scope yet; the one that first does adds its deny case here.
            DataScope.Dept or DataScope.Assigned or DataScope.Entity or DataScope.ReadOnly => throw new InvalidOperationException($"No deny case for {scope}."),
            _ => throw new InvalidOperationException($"No deny case for {scope}."),
        };

        Assert.Equal(expected, decision);
    }

    [Theory]
    [MemberData(nameof(WithheldCells))]
    public async Task AWithheldCellIsDenied(string role, string permission)
    {
        AuthorizationScenario scenario = Scenario(role);

        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await scenario.AuthorizeAsync(permission));
        Assert.Equal(AuthorizationOutcome.NotFound, (await scenario.AuthorizeAsync(permission, new AuthorizationSubject { OwnerUserId = UserId })).Outcome);
    }

    /// <summary>ADR-019, verbatim: granted to R02, R03 and R07 and withheld from R04, R05, R06 and R08.</summary>
    [Theory]
    [InlineData(PermissionCatalogue.LayoutPersonalize)]
    [InlineData(PermissionCatalogue.ReportCompose)]
    public void PersonalizeLayoutAndComposeReportGoToR02R03AndR07Only(string permission) =>
        Assert.Equal(["R02", "R03", "R07"], RoleCodes.Where(role => ShippedGrant(role, permission) is not null));

    [Fact]
    public void OnlyR01ManagesIdentityIntegration() =>
        Assert.Equal(["R01"], RoleCodes.Where(role => ShippedGrant(role, PermissionCatalogue.IdentityIntegrationManage) is not null));

    [Fact]
    public void EveryShippedGrantNamesACataloguePermissionAndACanonicalRole() =>
        Assert.All(PermissionCatalogue.ShippedDefaultGrants, grant =>
        {
            Assert.True(PermissionCatalogue.Platform.Contains(grant.PermissionCode), grant.PermissionCode);
            Assert.Contains(grant.RoleCode, RoleCodes);
        });

    private static ShippedGrant? ShippedGrant(string role, string permission) =>
        PermissionCatalogue.ShippedDefaultGrants.SingleOrDefault(g => g.RoleCode == role && g.PermissionCode == permission);

    private static AuthorizationScenario Disabled(AuthorizationScenario scenario)
    {
        scenario.Repository.Principals[UserId] = scenario.Repository.Principals[UserId] with { IsActive = false };
        return scenario;
    }

    private static AuthorizationScenario Scenario(string role) =>
        new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(
            UserTypeOf(role),
            [.. PermissionCatalogue.ShippedDefaultGrants.Where(g => g.RoleCode == role).Select(g => Grant(role, g.PermissionCode, g.Scope))]);
}
