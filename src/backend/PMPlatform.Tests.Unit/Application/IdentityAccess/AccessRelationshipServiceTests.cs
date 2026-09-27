using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.IdentityAccess.AdministrationFixture;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>ADM-010 (TASK-031): profile assignment (ADR-018) and the ADR-013 rules for external users and per-project access.</summary>
public sealed class AccessRelationshipServiceTests
{
    private static readonly Guid InternalUserId = Guid.Parse("00000000-0000-4000-8000-00000000a101");
    private static readonly Guid ExternalUserId = Guid.Parse("00000000-0000-4000-8000-00000000a102");
    private static readonly Guid SponsorId = Guid.Parse("00000000-0000-4000-8000-00000000a103");

    private readonly AdministrationFixture _fixture = new();

    public AccessRelationshipServiceTests()
    {
        _fixture.Users.Put(Internal(InternalUserId));
        _fixture.Users.Put(External(ExternalUserId));
        _fixture.Users.Put(Internal(SponsorId));
    }

    /// <summary>ADR-018: a profile version, and only a PUBLISHED one; there is no other way to give a user a permission (BR-IAM-023).</summary>
    [Fact]
    public async Task OnlyAPublishedProfileVersionIsAssigned()
    {
        AdministrationResult<AccessRelationshipDetail> draft = await CreateAsync(Draft(InternalUserId, DraftR06Version));
        AdministrationResult<AccessRelationshipDetail> missing = await CreateAsync(Draft(InternalUserId, Guid.NewGuid()));
        AdministrationResult<AccessRelationshipDetail> published = await CreateAsync(Draft(InternalUserId, VersionOf(6)));

        Assert.Equal(IdentityAccessErrorCodes.ProfileVersionNotPublished, draft.Error!.Code);
        Assert.Equal([new FieldIssue("permissionProfileVersionId", FieldIssue.NotFound)], missing.Error!.Fields);
        Assert.Equal(("R06", AccessRelationshipStatus.Active, Now, AdministratorId), (published.Value!.RoleCode, published.Value.Status, published.Value.StartsAt, published.Value.CreatedBy));
    }

    /// <summary>ADR-013, the entity Project Manager: R04 on a project their entity delivers, with a named AHDA sponsor.</summary>
    [Fact]
    public async Task AnEntityProjectManagerIsGrantedR04OnTheirOwnProject()
    {
        AdministrationResult<AccessRelationshipDetail> result = await CreateAsync(Draft(ExternalUserId, VersionOf(4)) with { ProjectId = ProjectId, SponsorUserId = SponsorId });

        Assert.Equal("R04", result.Value!.RoleCode);
        Assert.Equal((ProjectId, EntityId, SponsorId), (result.Value.ProjectId, result.Value.ExternalEntityId, result.Value.SponsorUserId));
    }

    public static TheoryData<string, AccessRelationshipDraft, FieldIssue[]> ExternalGrantViolations() => new()
    {
        { "internal-only role", Draft(ExternalUserId, VersionOf(3)) with { SponsorUserId = SponsorId }, [new("permissionProfileVersionId", FieldIssue.NotExternalEligible)] },
        { "no sponsor", Draft(ExternalUserId, VersionOf(8)), [new("sponsorUserId", FieldIssue.Required)] },
        { "R04 without a project", Draft(ExternalUserId, VersionOf(4)) with { SponsorUserId = SponsorId }, [new("projectId", FieldIssue.Required)] },
        {
            "another entity's project", Draft(ExternalUserId, VersionOf(4)) with { ProjectId = OtherEntityProjectId, SponsorUserId = SponsorId },
            [new("projectId", FieldIssue.OutsideEntity)]
        },
        {
            "another entity as anchor", Draft(ExternalUserId, VersionOf(8)) with { ExternalEntityId = OtherEntityId, SponsorUserId = SponsorId },
            [new("externalEntityId", FieldIssue.OutsideEntity)]
        },
        {
            "a department anchor", Draft(ExternalUserId, VersionOf(8)) with { DepartmentId = AdministrationFixture.DepartmentId, SponsorUserId = SponsorId },
            [new("departmentId", FieldIssue.NotAllowed)]
        },
        {
            "everything at once", Draft(ExternalUserId, VersionOf(1)) with { DepartmentId = AdministrationFixture.DepartmentId },
            [new("permissionProfileVersionId", FieldIssue.NotExternalEligible), new("departmentId", FieldIssue.NotAllowed), new("sponsorUserId", FieldIssue.Required)]
        },
    };

    [Theory]
    [MemberData(nameof(ExternalGrantViolations))]
    public async Task AnExternalGrantBreakingADR013IsRefusedWithEveryRuleItBreaks(string because, AccessRelationshipDraft draft, FieldIssue[] issues)
    {
        AdministrationResult<AccessRelationshipDetail> result = await CreateAsync(draft);

        Assert.True(result.Error is not null, because);
        Assert.Equal(IdentityAccessErrorCodes.ExternalGrantInvalid, result.Error.Code);
        Assert.Equal(issues, result.Error.Fields);
        Assert.Empty(_fixture.Assignments.Rows);
    }

    /// <summary>ADR-013: the sponsor is a named AHDA person, so an active internal user.</summary>
    [Fact]
    public async Task TheSponsorIsAnActiveInternalUser()
    {
        Guid disabledSponsorId = Guid.Parse("00000000-0000-4000-8000-00000000a104");
        User disabled = Internal(disabledSponsorId);
        disabled.Status = UserStatus.Disabled;
        _fixture.Users.Put(disabled);

        AdministrationResult<AccessRelationshipDetail> external = await CreateAsync(Draft(ExternalUserId, VersionOf(8)) with { SponsorUserId = ExternalUserId });
        AdministrationResult<AccessRelationshipDetail> disabledSponsor = await CreateAsync(Draft(ExternalUserId, VersionOf(8)) with { SponsorUserId = disabledSponsorId });

        Assert.Equal([new FieldIssue("sponsorUserId", FieldIssue.Inactive)], external.Error!.Fields);
        Assert.Equal([new FieldIssue("sponsorUserId", FieldIssue.Inactive)], disabledSponsor.Error!.Fields);
        Assert.Equal(IdentityAccessErrorCodes.ReferenceInvalid, external.Error.Code);
    }

    /// <summary>ADR-013: a role change on a project ends the access the previous role gave there; other assignments stand.</summary>
    [Fact]
    public async Task ANewRoleOnAProjectEndsThePreviousOneAsARoleChange()
    {
        AccessRelationshipDraft asManager = Draft(ExternalUserId, VersionOf(4)) with { ProjectId = ProjectId, SponsorUserId = SponsorId };
        Guid manager = (await CreateAsync(asManager)).Value!.Id;
        Guid entityWide = (await CreateAsync(Draft(ExternalUserId, VersionOf(8)) with { SponsorUserId = SponsorId })).Value!.Id;

        AdministrationResult<AccessRelationshipDetail> asContributor = await CreateAsync(asManager with { PermissionProfileVersionId = VersionOf(8) });

        AccessRelationship ended = _fixture.Assignments.Rows[manager];
        Assert.Equal((AccessRelationshipStatus.Ended, AccessEndReason.RoleChange, Now), (ended.Status, ended.EndReason, ended.EndsAt));
        Assert.Equal(AccessRelationshipStatus.Active, _fixture.Assignments.Rows[entityWide].Status);
        Assert.Equal(AccessRelationshipStatus.Active, asContributor.Value!.Status);
    }

    [Fact]
    public async Task AnIdenticalActiveAssignmentIsAConflict()
    {
        await CreateAsync(Draft(InternalUserId, VersionOf(6)));

        AdministrationResult<AccessRelationshipDetail> again = await CreateAsync(Draft(InternalUserId, VersionOf(6)));

        Assert.Equal((AdministrationErrorKind.Conflict, IdentityAccessErrorCodes.AlreadyAssigned), (again.Error!.Kind, again.Error.Code));
        Assert.Single(_fixture.Assignments.Rows);
    }

    [Fact]
    public async Task NoAdministratorAssignsThemselvesAndNoServicePrincipalIsAssigned()
    {
        Guid serviceId = Guid.Parse("00000000-0000-4000-8000-00000000a105");
        User service = Internal(serviceId);
        service.UserType = UserType.Service;
        _fixture.Users.Put(service);

        AdministrationResult<AccessRelationshipDetail> self = await CreateAsync(Draft(AdministratorId, VersionOf(2)));
        AdministrationResult<AccessRelationshipDetail> toService = await CreateAsync(Draft(serviceId, VersionOf(2)));

        Assert.Equal(IdentityAccessErrorCodes.SelfAdministration, self.Error!.Code);
        Assert.Equal(IdentityAccessErrorCodes.ServicePrincipal, toService.Error!.Code);
    }

    [Fact]
    public async Task AClosedProjectTakesNoNewAccess()
    {
        AdministrationResult<AccessRelationshipDetail> result = await CreateAsync(Draft(ExternalUserId, VersionOf(4)) with { ProjectId = ClosedProjectId, SponsorUserId = SponsorId });

        Assert.Equal(IdentityAccessErrorCodes.ProjectClosed, result.Error!.Code);
    }

    /// <summary>An assignment starts now or later and ends after it starts: access is never recorded for a time already past.</summary>
    [Fact]
    public async Task AnAssignmentStartsNoEarlierThanNowAndEndsAfterItStarts()
    {
        AdministrationResult<AccessRelationshipDetail> backdated = await CreateAsync(Draft(InternalUserId, VersionOf(6)) with { StartsAt = Now.AddDays(-1) });
        AdministrationResult<AccessRelationshipDetail> empty = await CreateAsync(Draft(InternalUserId, VersionOf(6)) with { StartsAt = Now, EndsAt = Now });
        AdministrationResult<AccessRelationshipDetail> future = await CreateAsync(Draft(InternalUserId, VersionOf(6)) with { StartsAt = Now.AddDays(1), EndsAt = Now.AddDays(2) });

        Assert.Equal((IdentityAccessErrorCodes.InvalidPeriod, new FieldIssue("startsAt", FieldIssue.NotAllowed)), (backdated.Error!.Code, backdated.Error.Fields.Single()));
        Assert.Equal((IdentityAccessErrorCodes.InvalidPeriod, new FieldIssue("endsAt", FieldIssue.BeforeStart)), (empty.Error!.Code, empty.Error.Fields.Single()));
        Assert.Equal(Now.AddDays(1), future.Value!.StartsAt);
    }

    [Fact]
    public async Task EndingAnAssignmentKeepsItAsEndedAndOnlyOnce()
    {
        Guid id = (await CreateAsync(Draft(InternalUserId, VersionOf(6)))).Value!.Id;

        AdministrationResult<AccessRelationshipDetail> ended = await _fixture.AssignmentService().EndAsync(AdministratorId, id, CancellationToken.None);
        AdministrationResult<AccessRelationshipDetail> again = await _fixture.AssignmentService().EndAsync(AdministratorId, id, CancellationToken.None);

        Assert.Equal((AccessRelationshipStatus.Ended, AccessEndReason.Manual, Now), (ended.Value!.Status, ended.Value.EndReason, ended.Value.EndsAt));
        Assert.Equal(AdministrationErrorKind.TerminalState, again.Error!.Kind);
        Assert.Single(_fixture.Assignments.Rows);
    }

    [Fact]
    public async Task NoAdministratorEndsTheirOwnAssignment()
    {
        AccessRelationship own = new()
        {
            Id = Guid.NewGuid(),
            UserId = AdministratorId,
            PermissionProfileVersionId = VersionOf(1),
            StartsAt = Now.AddDays(-1),
            Status = AccessRelationshipStatus.Active,
        };
        _fixture.Assignments.Put(own);

        AdministrationResult<AccessRelationshipDetail> result = await _fixture.AssignmentService().EndAsync(AdministratorId, own.Id, CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.SelfAdministration, result.Error!.Code);
        Assert.Equal(AccessRelationshipStatus.Active, own.Status);
    }

    /// <summary>ADR-013: project closure ends every active assignment on that project, and nothing else.</summary>
    [Fact]
    public async Task ClosingAProjectEndsItsAccessAndOnlyItsAccess()
    {
        Guid onProject = (await CreateAsync(Draft(ExternalUserId, VersionOf(4)) with { ProjectId = ProjectId, SponsorUserId = SponsorId })).Value!.Id;
        Guid internalOnProject = (await CreateAsync(Draft(InternalUserId, VersionOf(4)) with { ProjectId = ProjectId })).Value!.Id;
        Guid elsewhere = (await CreateAsync(Draft(InternalUserId, VersionOf(6)))).Value!.Id;

        int ended = await _fixture.AssignmentService().EndAccessForClosedProjectAsync(ProjectId, AdministratorId, CancellationToken.None);

        Assert.Equal(2, ended);
        Assert.All([onProject, internalOnProject], id => Assert.Equal(
            (AccessRelationshipStatus.Ended, AccessEndReason.ProjectClosed), (_fixture.Assignments.Rows[id].Status, _fixture.Assignments.Rows[id].EndReason)));
        Assert.Equal(AccessRelationshipStatus.Active, _fixture.Assignments.Rows[elsewhere].Status);
    }

    private static AccessRelationshipDraft Draft(Guid userId, Guid versionId) => new(userId, versionId, null, null, null, null, null, null);

    private Task<AdministrationResult<AccessRelationshipDetail>> CreateAsync(AccessRelationshipDraft draft) =>
        _fixture.AssignmentService().CreateAsync(AdministratorId, draft, CancellationToken.None);
}
