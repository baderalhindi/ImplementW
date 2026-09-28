using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.IdentityAccess.AdministrationFixture;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>
/// TASK-033, CTL-25: each FG-03 change stages its audit event for the save that commits the change, and a refused change
/// stages none. The engine's own refusals are recorded, not staged.
/// </summary>
public sealed class AdministrationAuditTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-00000000a201");
    private static readonly Guid ExternalUserId = Guid.Parse("00000000-0000-4000-8000-00000000a202");
    private static readonly Guid SponsorId = Guid.Parse("00000000-0000-4000-8000-00000000a203");

    private readonly AdministrationFixture _fixture = new();

    [Fact]
    public async Task DisablingAUserStagesItsPrivilegedActionEvent()
    {
        _fixture.Users.Put(Internal(UserId));

        await _fixture.UserService().DisableAsync(AdministratorId, UserId, expectedVersion: null, CancellationToken.None);

        AuditEntry staged = Assert.Single(_fixture.Audit.Staged);
        Assert.Equal((AuditEventClass.PrivilegedAction, "IdentityAccess.UserDisabled", AuditOutcome.Success), (staged.EventClass, staged.EventType, staged.Outcome));
        Assert.Equal((AdministratorId, new AuditSubject("IdentityAccess", "User", UserId)), (staged.ActorUserId, staged.Subject));
        Assert.Equal([new AuditAttribute("status", "ACTIVE", "DISABLED")], staged.Attributes);
    }

    /// <summary>A change refused by a rule is not an action taken: nothing is staged, and the engine allowed it, so nothing is recorded.</summary>
    [Fact]
    public async Task ARefusedChangeStagesNothing()
    {
        await _fixture.UserService().DisableAsync(AdministratorId, AdministratorId, expectedVersion: null, CancellationToken.None);

        Assert.Empty(_fixture.Audit.All);
    }

    [Fact]
    public async Task AnUpdateRecordsWhichContactDetailsChangedButNotTheirValues()
    {
        User user = Internal(UserId);
        user.MobileNumber = "+966500000001";
        _fixture.Users.Put(user);
        UserChanges changes = new(user.Username, "Renamed user", "new.address@identity.test", "+966500000002", Language.En, user.DirectorySubjectId, user.JobTitle);

        await _fixture.UserService().UpdateAsync(AdministratorId, UserId, changes, expectedVersion: 0, CancellationToken.None);

        AuditEntry staged = Assert.Single(_fixture.Audit.Staged);
        Assert.Equal("IdentityAccess.UserUpdated", staged.EventType);
        Assert.Equal(
            [
                new AuditAttribute("display_name", $"User {UserId}", "Renamed user"),
                new AuditAttribute("email", AuditAttribute.Withheld, AuditAttribute.Withheld),
                new AuditAttribute("mobile_number", AuditAttribute.Withheld, AuditAttribute.Withheld),
                new AuditAttribute("preferred_language", "AR", "EN"),
            ],
            staged.Attributes);
    }

    /// <summary>ADR-013's role change is two permission changes, staged together: the old assignment ended and the new one made.</summary>
    [Fact]
    public async Task ARoleChangeStagesTheEndOfTheOldRoleAndTheNewOne()
    {
        _fixture.Users.Put(External(ExternalUserId));
        _fixture.Users.Put(Internal(SponsorId));
        AccessRelationshipDraft asManager = new(ExternalUserId, VersionOf(4), null, null, ProjectId, SponsorId, null, null);
        Guid manager = (await _fixture.AssignmentService().CreateAsync(AdministratorId, asManager, CancellationToken.None)).Value!.Id;
        _fixture.Audit.Staged.Clear();

        Guid contributor = (await _fixture.AssignmentService().CreateAsync(AdministratorId, asManager with { PermissionProfileVersionId = VersionOf(8) }, CancellationToken.None)).Value!.Id;

        Assert.Equal(
            [("IdentityAccess.RoleAssignmentEnded", manager), ("IdentityAccess.RoleAssigned", contributor)],
            _fixture.Audit.Staged.Select(e => (e.EventType, e.Subject!.Id)));
        Assert.All(_fixture.Audit.Staged, e => Assert.Equal((AuditEventClass.PermissionChange, ProjectId, EntityId), (e.EventClass, e.ScopeProjectId, e.ScopeExternalEntityId)));
        Assert.Contains(new AuditAttribute("end_reason", null, "ROLE_CHANGE"), _fixture.Audit.Staged[0].Attributes);
        Assert.Contains(new AuditAttribute("role_code", null, "R08"), _fixture.Audit.Staged[1].Attributes);
    }
}
