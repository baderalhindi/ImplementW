using Microsoft.Extensions.Logging.Abstractions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>An engine over a fake repository, a test catalogue and the ids the tests share.</summary>
internal sealed class AuthorizationScenario
{
    /// <summary>Reads a record of the TEST group.</summary>
    public const string Read = "TEST_READ";

    /// <summary>Changes a record of the TEST group.</summary>
    public const string Write = "TEST_WRITE";

    /// <summary>Another change to a TEST record, never granted in these tests.</summary>
    public const string Approve = "TEST_APPROVE";

    /// <summary>Reads a record of another group.</summary>
    public const string OtherRead = "OTHER_READ";

    public static readonly string[] RoleCodes = ["R01", "R02", "R03", "R04", "R05", "R06", "R07", "R08"];

    public static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-000000000a01");
    public static readonly Guid OtherUserId = Guid.Parse("00000000-0000-4000-8000-000000000a02");
    public static readonly Guid DepartmentId = Guid.Parse("00000000-0000-4000-8000-000000000b01");
    public static readonly Guid OtherDepartmentId = Guid.Parse("00000000-0000-4000-8000-000000000b02");
    public static readonly Guid EntityId = Guid.Parse("00000000-0000-4000-8000-000000000c01");
    public static readonly Guid OtherEntityId = Guid.Parse("00000000-0000-4000-8000-000000000c02");
    public static readonly Guid ProjectId = Guid.Parse("00000000-0000-4000-8000-000000000d01");
    public static readonly Guid OtherProjectId = Guid.Parse("00000000-0000-4000-8000-000000000d02");

    public static readonly PermissionCatalogue Catalogue = new(
        [
            new(Read, "TEST", AccessMode.Read),
            new(Write, "TEST", AccessMode.Write),
            new(Approve, "TEST", AccessMode.Write),
            new(OtherRead, "OTHER", AccessMode.Read),
        ]);

    public FakeAuthorizationRepository Repository { get; } = new();

    public AuthorizationScenario(PermissionCatalogue? catalogue = null)
    {
        Engine = new AuthorizationEngine(Repository, catalogue ?? Catalogue, NullLogger<AuthorizationEngine>.Instance);
    }

    public IAuthorizationEngine Engine { get; }

    /// <summary>The user, active, holding <paramref name="grants"/>. R08 is an external user of <see cref="EntityId"/> (ADR-013).</summary>
    public AuthorizationScenario WithUser(UserType userType, params EffectiveGrant[] grants)
    {
        Repository.Principals[UserId] = new AuthorizationPrincipal(
            UserId, userType, IsActive: true, userType == UserType.External ? null : DepartmentId, userType == UserType.External ? EntityId : null, grants);
        return this;
    }

    public static UserType UserTypeOf(string roleCode) => roleCode == "R08" ? UserType.External : UserType.Internal;

    public static EffectiveGrant Grant(
        string roleCode, string permissionCode, DataScope scope, Guid? departmentId = null, Guid? entityId = null, Guid? projectId = null, Guid? clearanceItemId = null) =>
        new(roleCode, permissionCode, scope, departmentId, entityId, projectId, clearanceItemId);

    public Task<AuthorizationDecision> AuthorizeAsync(string permissionCode, AuthorizationSubject? subject = null) =>
        Engine.AuthorizeAsync(UserId, new AuthorizationRequest(permissionCode, subject), CancellationToken.None);
}
