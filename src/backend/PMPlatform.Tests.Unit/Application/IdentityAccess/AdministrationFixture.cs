using Microsoft.Extensions.Logging.Abstractions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Tests.Unit.Application.Auditing;
using PMPlatform.Tests.Unit.Application.Authorization;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>
/// The administration services over in-memory repositories and the real authorization engine. The administrator holds R01's
/// shipped grants, as db/seed gives them; other callers hold what a test gives them.
/// </summary>
internal sealed class AdministrationFixture
{
    public static readonly Guid AdministratorId = Guid.Parse("00000000-0000-4000-8000-00000000a001");
    public static readonly Guid DepartmentId = Guid.Parse("00000000-0000-4000-8000-00000000b001");
    public static readonly Guid OtherDepartmentId = Guid.Parse("00000000-0000-4000-8000-00000000b002");
    public static readonly Guid EntityId = Guid.Parse("00000000-0000-4000-8000-00000000c001");
    public static readonly Guid OtherEntityId = Guid.Parse("00000000-0000-4000-8000-00000000c002");
    public static readonly Guid RetiredEntityId = Guid.Parse("00000000-0000-4000-8000-00000000c003");
    public static readonly Guid EntityTypeItemId = Guid.Parse("00000000-0000-4000-8000-00000000e001");
    public static readonly Guid ProjectId = Guid.Parse("00000000-0000-4000-8000-00000000d001");
    public static readonly Guid OtherEntityProjectId = Guid.Parse("00000000-0000-4000-8000-00000000d002");
    public static readonly Guid ClosedProjectId = Guid.Parse("00000000-0000-4000-8000-00000000d003");

    /// <summary>The PUBLISHED version 1 of role R0<paramref name="n"/>'s shipped default.</summary>
    public static Guid VersionOf(int n) => Guid.Parse($"00000000-0002-4000-8000-{n:D12}");

    public static readonly Guid DraftR06Version = Guid.Parse("00000000-0002-4000-8000-000000000106");

    public static readonly DateTimeOffset Now = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    public AdministrationFixture()
    {
        foreach (int n in Enumerable.Range(1, 8))
        {
            Assignments.ProfileVersions[VersionOf(n)] = new ProfileVersionFacts(GovernedLifecycleState.Published, $"R0{n}", n is 4 or 8);
        }

        Assignments.ProfileVersions[DraftR06Version] = new ProfileVersionFacts(GovernedLifecycleState.Draft, "R06", false);
        Assignments.Projects[ProjectId] = new ProjectFacts(EntityId, IsClosed: false);
        Assignments.Projects[OtherEntityProjectId] = new ProjectFacts(OtherEntityId, IsClosed: false);
        Assignments.Projects[ClosedProjectId] = new ProjectFacts(EntityId, IsClosed: true);

        Departments.Put(new Department { Id = DepartmentId, Code = "DEPT-A", Name = Label("A"), DirectoryReference = "OU-A" });
        Departments.Put(new Department { Id = OtherDepartmentId, Code = "DEPT-B", Name = Label("B") });
        Entities.Put(new ExternalEntity { Id = EntityId, Code = "ENT-A", Name = Label("A"), EntityTypeItemId = EntityTypeItemId, Status = ExternalEntityStatus.Active });
        Entities.Put(new ExternalEntity { Id = OtherEntityId, Code = "ENT-B", Name = Label("B"), EntityTypeItemId = EntityTypeItemId, Status = ExternalEntityStatus.Active });
        Entities.Put(new ExternalEntity { Id = RetiredEntityId, Code = "ENT-C", Name = Label("C"), EntityTypeItemId = EntityTypeItemId, Status = ExternalEntityStatus.Retired });
        Entities.EntityTypes.Add(EntityTypeItemId);

        Users.Put(Internal(AdministratorId));
        Authorization.Principals[AdministratorId] = Principal(
            AdministratorId, UserType.Internal, DepartmentId, null,
            [.. PermissionCatalogue.ShippedDefaultGrants.Where(g => g.RoleCode == "R01").Select(g => AuthorizationScenario.Grant("R01", g.PermissionCode, g.Scope))]);
    }

    public FakeAuthorizationRepository Authorization { get; } = new();

    public FakeUserAdministrationRepository Users { get; } = new();

    public FakeAccessRelationshipRepository Assignments { get; } = new();

    public FakeDepartmentRepository Departments { get; } = new();

    public FakeExternalEntityRepository Entities { get; } = new();

    public FakeMobileNumberVerifier Verifier { get; } = new();

    public FixedTimeProvider Clock { get; } = new(Now);

    public RecordingAuditTrail Audit { get; } = new();

    public static BilingualLabel Label(string name) => new($"اسم {name}", name);

    public static User Internal(Guid id, Guid? departmentId = null) => new()
    {
        Id = id,
        UserType = UserType.Internal,
        DirectorySubjectId = $"subject-{id}",
        Username = $"user-{id}",
        DisplayName = $"User {id}",
        Email = $"{id}@identity.test",
        JobTitle = "Directory title",
        DepartmentId = departmentId ?? DepartmentId,
        Status = UserStatus.Active,
    };

    public static User External(Guid id, Guid? entityId = null) => new()
    {
        Id = id,
        UserType = UserType.External,
        Username = $"user-{id}",
        DisplayName = $"User {id}",
        Email = $"{id}@entity.test",
        ExternalEntityId = entityId ?? EntityId,
        Status = UserStatus.Active,
    };

    public static AuthorizationPrincipal Principal(Guid id, UserType userType, Guid? departmentId, Guid? entityId, IReadOnlyList<EffectiveGrant> grants) =>
        new(id, userType, IsActive: true, departmentId, entityId, grants);

    public UserAdministrationService UserService() =>
        new(Users, Entities, Access(), Audit, Clock, NullLogger<UserAdministrationService>.Instance);

    public AccessRelationshipService AssignmentService() =>
        new(Assignments, Users, Departments, Entities, Access(), Audit, Clock, NullLogger<AccessRelationshipService>.Instance);

    public DepartmentAdministrationService DepartmentService() =>
        new(Departments, Access(), Audit, Clock, NullLogger<DepartmentAdministrationService>.Instance);

    public ExternalEntityAdministrationService EntityService() =>
        new(Entities, Users, Access(), Audit, Clock, NullLogger<ExternalEntityAdministrationService>.Instance);

    public MobileNumberVerificationService MobileService() =>
        new(Users, Verifier, Clock, NullLogger<MobileNumberVerificationService>.Instance);

    /// <summary>A fresh engine per service, as one request would have.</summary>
    private AdministrationAccess Access() =>
        new(new AuthorizationEngine(Authorization, PermissionCatalogue.Platform, Audit, NullLogger<AuthorizationEngine>.Instance));
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
