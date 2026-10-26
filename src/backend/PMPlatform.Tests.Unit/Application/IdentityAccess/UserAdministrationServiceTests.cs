using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.IdentityAccess.AdministrationFixture;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>ADM-002–005 and MOD-080 (TASK-031): the user lifecycle and the rules each command applies.</summary>
public sealed class UserAdministrationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-00000000a101");

    private readonly AdministrationFixture _fixture = new();

    /// <summary>Appendix A.1: disabling writes the status and its time, and not one other attribute of the user.</summary>
    [Fact]
    public async Task DisablingAUserChangesOnlyTheirStatus()
    {
        User user = Internal(UserId);
        user.MobileNumber = "+966500000001";
        user.MobileVerifiedAt = Now.AddDays(-3);
        user.ManagerUserId = AdministratorId;
        _fixture.Users.Put(user);
        UserState before = Copy(user);

        AdministrationResult<Versioned<UserDetail>> result = await _fixture.UserService().DisableAsync(AdministratorId, UserId, expectedVersion: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(UserStatus.Disabled, result.Value.Value.Status);
        Assert.Equal(Now, result.Value.Value.DisabledAt);
        Assert.Equal(
            before with { Status = UserStatus.Disabled, DisabledAt = Now, UpdatedAt = Now, UpdatedBy = AdministratorId },
            Copy(user));
    }

    [Fact]
    public async Task ActivatingRestoresTheUserAndClearsTheDisabledTime()
    {
        User user = Internal(UserId);
        user.Status = UserStatus.Disabled;
        user.DisabledAt = Now.AddDays(-1);
        _fixture.Users.Put(user);

        AdministrationResult<Versioned<UserDetail>> result = await _fixture.UserService().ActivateAsync(AdministratorId, UserId, expectedVersion: null, CancellationToken.None);

        Assert.Equal(UserStatus.Active, result.Value!.Value.Status);
        Assert.Null(result.Value.Value.DisabledAt);
    }

    [Fact]
    public async Task EachLifecycleCommandIsAllowedFromOneStatusOnly()
    {
        _fixture.Users.Put(Internal(UserId));
        UserAdministrationService service = _fixture.UserService();

        AdministrationResult<Versioned<UserDetail>> activateActive = await service.ActivateAsync(AdministratorId, UserId, null, CancellationToken.None);
        await service.DisableAsync(AdministratorId, UserId, null, CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> disableDisabled = await _fixture.UserService().DisableAsync(AdministratorId, UserId, null, CancellationToken.None);

        Assert.Equal(AdministrationErrorKind.InvalidTransition, activateActive.Error!.Kind);
        Assert.Equal(AdministrationErrorKind.InvalidTransition, disableDisabled.Error!.Kind);
    }

    [Fact]
    public async Task AnAdministratorCannotDisableThemselves()
    {
        AdministrationResult<Versioned<UserDetail>> result = await _fixture.UserService().DisableAsync(AdministratorId, AdministratorId, null, CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.SelfAdministration, result.Error!.Code);
        Assert.Equal(UserStatus.Active, _fixture.Users.Rows[AdministratorId].Status);
    }

    [Fact]
    public async Task AServicePrincipalIsNotAdministered()
    {
        User service = Internal(UserId);
        service.UserType = UserType.Service;
        _fixture.Users.Put(service);

        AdministrationResult<Versioned<UserDetail>> disable = await _fixture.UserService().DisableAsync(AdministratorId, UserId, null, CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> create = await _fixture.UserService().CreateAsync(AdministratorId, Draft(UserType.Service), CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.ServicePrincipal, disable.Error!.Code);
        Assert.Equal(IdentityAccessErrorCodes.ServicePrincipal, create.Error!.Code);
    }

    /// <summary>ADR-004: a new number is unverified until its holder confirms it; an unchanged one keeps its verification.</summary>
    [Fact]
    public async Task ChangingTheMobileNumberUnverifiesItAndKeepingItDoesNot()
    {
        User user = Internal(UserId);
        user.MobileNumber = "+966500000001";
        user.MobileVerifiedAt = Now.AddDays(-3);
        _fixture.Users.Put(user);

        AdministrationResult<Versioned<UserDetail>> kept = await _fixture.UserService().UpdateAsync(
            AdministratorId, UserId, Changes(user, "+966500000001"), _fixture.Users.Versions[UserId], CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> changed = await _fixture.UserService().UpdateAsync(
            AdministratorId, UserId, Changes(user, "+966500000002"), _fixture.Users.Versions[UserId], CancellationToken.None);

        Assert.Equal(Now.AddDays(-3), kept.Value!.Value.MobileVerifiedAt);
        Assert.Equal("+966500000002", changed.Value!.Value.MobileNumber);
        Assert.Null(changed.Value.Value.MobileVerifiedAt);
    }

    /// <summary>ADR-007: an internal user's job title is the directory's, on create and on edit; an external user's is entered.</summary>
    [Fact]
    public async Task AnInternalUsersJobTitleIsTheDirectorys()
    {
        User user = Internal(UserId);
        _fixture.Users.Put(user);

        AdministrationResult<Versioned<UserDetail>> edit = await _fixture.UserService().UpdateAsync(
            AdministratorId, UserId, Changes(user, null) with { JobTitle = "Entered title" }, _fixture.Users.Versions[UserId], CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> create = await _fixture.UserService().CreateAsync(
            AdministratorId, Draft(UserType.Internal) with { JobTitle = "Entered title" }, CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> external = await _fixture.UserService().CreateAsync(
            AdministratorId, Draft(UserType.External) with { JobTitle = "Entity title", ExternalEntityId = EntityId }, CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.DirectoryAuthoritative, edit.Error!.Code);
        Assert.Equal([new FieldIssue("jobTitle", FieldIssue.NotAllowed)], edit.Error.Fields);
        Assert.Equal(IdentityAccessErrorCodes.DirectoryAuthoritative, create.Error!.Code);
        Assert.Equal("Entity title", external.Value!.Value.JobTitle);
    }

    [Theory]
    [InlineData("00000000-0000-4000-8000-00000000c003", FieldIssue.Inactive)]
    [InlineData("00000000-0000-4000-8000-00000000c0ff", FieldIssue.NotFound)]
    public async Task AnExternalUserBelongsToAnUnretiredEntity(string entityId, string issue)
    {
        AdministrationResult<Versioned<UserDetail>> result = await _fixture.UserService().CreateAsync(
            AdministratorId, Draft(UserType.External) with { ExternalEntityId = Guid.Parse(entityId) }, CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.ReferenceInvalid, result.Error!.Code);
        Assert.Equal([new FieldIssue("externalEntityId", issue)], result.Error.Fields);
    }

    [Fact]
    public async Task ACreatedUserIsActiveAndAttributedToTheAdministrator()
    {
        AdministrationResult<Versioned<UserDetail>> result = await _fixture.UserService().CreateAsync(AdministratorId, Draft(UserType.Internal), CancellationToken.None);

        UserDetail user = result.Value!.Value;
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal("en", user.PreferredLanguage);
        Assert.Null(user.MobileVerifiedAt);
        Assert.Equal((AdministratorId, Now, AdministratorId, Now), (user.CreatedBy, user.CreatedAt, user.UpdatedBy, user.UpdatedAt));
    }

    /// <summary>R-21: an edit made against a version the row no longer has is refused, and so is a taken unique key.</summary>
    [Fact]
    public async Task AStaleVersionAndATakenKeyAreRefused()
    {
        User user = Internal(UserId);
        _fixture.Users.Put(user);
        uint stale = _fixture.Users.Versions[UserId] + 7;

        AdministrationResult<Versioned<UserDetail>> staleEdit = await _fixture.UserService().UpdateAsync(AdministratorId, UserId, Changes(user, null), stale, CancellationToken.None);
        _fixture.Users.NextSaveResult = new SaveResult(SaveOutcome.DuplicateKey, "username");
        AdministrationResult<Versioned<UserDetail>> taken = await _fixture.UserService().CreateAsync(AdministratorId, Draft(UserType.Internal), CancellationToken.None);

        Assert.Equal(AdministrationErrorKind.PreconditionFailed, staleEdit.Error!.Kind);
        Assert.Equal(AdministrationErrorKind.Conflict, taken.Error!.Kind);
        Assert.Equal(IdentityAccessErrorCodes.DuplicateKey, taken.Error.Code);
        Assert.Equal([new FieldIssue("username", FieldIssue.Duplicate)], taken.Error.Fields);
    }

    /// <summary>R-47 on a record: 404 for a caller who cannot see it, 403 for one who can see it but not change it.</summary>
    [Fact]
    public async Task ACallerWithoutTheManagePermissionIsRefused()
    {
        Guid viewerId = Guid.Parse("00000000-0000-4000-8000-00000000a201");
        Guid strangerId = Guid.Parse("00000000-0000-4000-8000-00000000a202");
        _fixture.Users.Put(Internal(UserId));
        _fixture.Authorization.Principals[viewerId] = Principal(viewerId, UserType.Internal, DepartmentId, null, [Grant(PermissionCatalogue.UserView, DataScope.All)]);
        _fixture.Authorization.Principals[strangerId] = Principal(strangerId, UserType.Internal, DepartmentId, null, []);

        AdministrationResult<Versioned<UserDetail>> byViewer = await _fixture.UserService().DisableAsync(viewerId, UserId, null, CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> byStranger = await _fixture.UserService().DisableAsync(strangerId, UserId, null, CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> createByViewer = await _fixture.UserService().CreateAsync(viewerId, Draft(UserType.Internal), CancellationToken.None);

        Assert.Equal(AdministrationErrorKind.Forbidden, byViewer.Error!.Kind);
        Assert.Equal(AdministrationErrorKind.NotFound, byStranger.Error!.Kind);
        Assert.Equal(AdministrationErrorKind.Forbidden, createByViewer.Error!.Kind);
        Assert.Equal(UserStatus.Active, _fixture.Users.Rows[UserId].Status);
    }

    /// <summary>
    /// A DEPT-scoped USER_VIEW sees the users of its department only, and gets no list: the list cannot yet be filtered by
    /// scope (engine record F-8), so it is refused rather than shown in full.
    /// </summary>
    [Fact]
    public async Task ADepartmentScopedViewerSeesTheirDepartmentAndNoList()
    {
        Guid managerId = Guid.Parse("00000000-0000-4000-8000-00000000a301");
        Guid otherDepartmentUserId = Guid.Parse("00000000-0000-4000-8000-00000000a302");
        _fixture.Users.Put(Internal(UserId));
        _fixture.Users.Put(Internal(otherDepartmentUserId, OtherDepartmentId));
        _fixture.Authorization.Principals[managerId] = Principal(managerId, UserType.Internal, DepartmentId, null, [Grant(PermissionCatalogue.UserView, DataScope.Dept)]);
        UserQuery everyone = new([], [], null, null, null, UserSort.DisplayNameAscending, new PageRequest(1, 25));

        AdministrationResult<Versioned<UserDetail>> own = await _fixture.UserService().GetAsync(managerId, UserId, CancellationToken.None);
        AdministrationResult<Versioned<UserDetail>> other = await _fixture.UserService().GetAsync(managerId, otherDepartmentUserId, CancellationToken.None);
        AdministrationResult<UserPage> list = await _fixture.UserService().ListAsync(managerId, everyone, CancellationToken.None);
        AdministrationResult<UserPage> listByAdministrator = await _fixture.UserService().ListAsync(AdministratorId, everyone, CancellationToken.None);

        Assert.True(own.Succeeded);
        Assert.Equal(AdministrationErrorKind.NotFound, other.Error!.Kind);
        Assert.Equal(AdministrationErrorKind.Forbidden, list.Error!.Kind);
        Assert.Equal(3, listByAdministrator.Value!.TotalCount);
    }

    private static EffectiveGrant Grant(string permission, DataScope scope) => new("R0X", permission, scope, null, null, null, null);

    private static UserDraft Draft(UserType userType) =>
        new(userType, "new.user", "New User", "new.user@identity.test", null, Language.En, userType == UserType.Internal ? "subject-new" : null, null, null);

    private static UserChanges Changes(User user, string? mobileNumber) =>
        new(user.Username, user.DisplayName, user.Email, mobileNumber, user.PreferredLanguage, user.DirectorySubjectId, user.JobTitle);

    /// <summary>A value copy of every attribute, so two can be compared field by field.</summary>
    private static UserState Copy(User u) =>
        new(u.Id, u.UserType, u.DirectorySubjectId, u.Username, u.DisplayName, u.Email, u.MobileNumber, u.MobileVerifiedAt, u.JobTitle, u.DepartmentId, u.ManagerUserId,
            u.ExternalEntityId, u.PreferredLanguage, u.Status, u.DisabledAt, u.MfaEnrolledAt, u.NafathVerificationReference, u.NafathVerifiedAt, u.CreatedAt, u.CreatedBy,
            u.UpdatedAt, u.UpdatedBy);

    private sealed record UserState(
        Guid Id, UserType UserType, string? DirectorySubjectId, string Username, string DisplayName, string Email, string? MobileNumber, DateTimeOffset? MobileVerifiedAt,
        string? JobTitle, Guid? DepartmentId, Guid? ManagerUserId, Guid? ExternalEntityId, Language PreferredLanguage, UserStatus Status, DateTimeOffset? DisabledAt,
        DateTimeOffset? MfaEnrolledAt, string? NafathVerificationReference, DateTimeOffset? NafathVerifiedAt, DateTimeOffset CreatedAt, Guid CreatedBy,
        DateTimeOffset UpdatedAt, Guid UpdatedBy);
}
