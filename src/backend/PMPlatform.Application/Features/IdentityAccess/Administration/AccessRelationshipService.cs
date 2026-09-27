using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-010 Role Assignment (TASK-031). Assigns a PUBLISHED profile version, never a permission (ADR-018, BR-IAM-023), and
/// applies ADR-013 to external users: an external-eligible role, their own entity, a named AHDA sponsor, and R04 only per
/// project on a project their entity delivers. A user holds at most one active assignment per project: a new one ends the
/// old as ROLE_CHANGE. Assignments end; they are never deleted or rewritten.
/// </summary>
internal sealed partial class AccessRelationshipService(
    IAccessRelationshipRepository assignments,
    IUserAdministrationRepository users,
    IDepartmentRepository departments,
    IExternalEntityRepository entities,
    AdministrationAccess access,
    TimeProvider timeProvider,
    ILogger<AccessRelationshipService> logger) : IAccessRelationshipService, IProjectAccessLifecycle
{
    /// <summary>ADR-013: the entity Project Manager holds R04 scoped to their own project only.</summary>
    internal const string ProjectManagerRoleCode = "R04";

    public async Task<AdministrationResult<AccessRelationshipPage>> ListAsync(Guid actorId, AccessRelationshipQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await access.CheckCollectionAsync(actorId, PermissionCatalogue.UserView, cancellationToken).ConfigureAwait(false) is { } denied
            ? denied
            : await assignments.ListAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<AccessRelationshipDetail>> GetAsync(Guid actorId, Guid accessRelationshipId, CancellationToken cancellationToken)
    {
        AccessRelationshipDetail? assignment = await assignments.FindDetailAsync(accessRelationshipId, cancellationToken).ConfigureAwait(false);
        return assignment is null ? AdministrationError.NotFound
            : await access.CheckRecordAsync(actorId, PermissionCatalogue.UserView, AdministrationAccess.SubjectOf(assignment), cancellationToken).ConfigureAwait(false) is { } denied ? denied
            : assignment;
    }

    public async Task<AdministrationResult<AccessRelationshipDetail>> CreateAsync(Guid actorId, AccessRelationshipDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        UserDetail? user = (await users.FindDetailAsync(draft.UserId, cancellationToken).ConfigureAwait(false))?.Value;
        if (user is null)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue("userId", FieldIssue.NotFound));
        }

        bool external = user.UserType == UserType.External;
        Guid? entityAnchor = external ? user.ExternalEntityId : draft.ExternalEntityId;
        AuthorizationSubject subject = new()
        {
            DepartmentId = draft.DepartmentId ?? user.DepartmentId,
            ExternalEntityId = entityAnchor,
            ProjectId = draft.ProjectId,
            OwnerUserId = user.Id,
        };
        if (await access.CheckNewRecordAsync(actorId, PermissionCatalogue.RoleAssign, subject, cancellationToken).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        if (user.UserType == UserType.Service)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ServicePrincipal, new FieldIssue("userId", FieldIssue.NotAllowed));
        }

        // No administrator grants themselves access, and none withdraws their own.
        if (user.Id == actorId)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.SelfAdministration, new FieldIssue("userId", FieldIssue.NotAllowed));
        }

        ProfileVersionFacts? version = await assignments.FindProfileVersionAsync(draft.PermissionProfileVersionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue("permissionProfileVersionId", FieldIssue.NotFound));
        }

        if (version.LifecycleState != GovernedLifecycleState.Published)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ProfileVersionNotPublished, new FieldIssue("permissionProfileVersionId", FieldIssue.NotAllowed));
        }

        // An assignment starts now or later: a start in the past would record access the user never had.
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset startsAt = draft.StartsAt ?? now;
        if (startsAt < now)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.InvalidPeriod, new FieldIssue("startsAt", FieldIssue.NotAllowed));
        }

        if (draft.EndsAt is { } endsAt && endsAt <= startsAt)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.InvalidPeriod, new FieldIssue("endsAt", FieldIssue.BeforeStart));
        }

        ProjectFacts? project = draft.ProjectId is { } projectId
            ? await assignments.FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false)
            : null;
        if (await ReferenceIssuesAsync(draft, project, cancellationToken).ConfigureAwait(false) is { Length: > 0 } referenceIssues)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, referenceIssues);
        }

        if (external && ExternalGrantIssues(user, version, draft, project) is { Length: > 0 } externalIssues)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ExternalGrantInvalid, externalIssues);
        }

        if (project is { IsClosed: true })
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ProjectClosed, new FieldIssue("projectId", FieldIssue.NotAllowed));
        }

        IReadOnlyList<AccessRelationship> active = await assignments.FindActiveOfUserForUpdateAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (active.Any(a => a.PermissionProfileVersionId == draft.PermissionProfileVersionId
                            && a.DepartmentId == draft.DepartmentId
                            && a.ExternalEntityId == entityAnchor
                            && a.ProjectId == draft.ProjectId))
        {
            return AdministrationError.Conflict(IdentityAccessErrorCodes.AlreadyAssigned);
        }

        // ADR-013: a changed role on a project ends the access the old role gave.
        foreach (AccessRelationship superseded in active.Where(a => draft.ProjectId is not null && a.ProjectId == draft.ProjectId))
        {
            End(superseded, AccessEndReason.RoleChange, now, actorId);
        }

        AccessRelationship assignment = new()
        {
            Id = Guid.CreateVersion7(now),
            UserId = user.Id,
            PermissionProfileVersionId = draft.PermissionProfileVersionId,
            DepartmentId = draft.DepartmentId,
            ExternalEntityId = entityAnchor,
            ProjectId = draft.ProjectId,
            SponsorUserId = draft.SponsorUserId,
            StartsAt = startsAt,
            EndsAt = draft.EndsAt,
            Status = AccessRelationshipStatus.Active,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        assignments.Add(assignment);

        if ((await assignments.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogAssigned(logger, actorId, assignment.Id, user.Id, version.RoleCode);
        return await DetailAsync(assignment.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<AccessRelationshipDetail>> EndAsync(Guid actorId, Guid accessRelationshipId, CancellationToken cancellationToken)
    {
        AccessRelationship? assignment = await assignments.FindForUpdateAsync(accessRelationshipId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckRecordAsync(actorId, PermissionCatalogue.RoleAssign, AdministrationAccess.SubjectOf(assignment), cancellationToken)
                .ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        if (assignment.UserId == actorId)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.SelfAdministration);
        }

        if (assignment.Status == AccessRelationshipStatus.Ended)
        {
            return AdministrationError.TerminalState;
        }

        End(assignment, AccessEndReason.Manual, timeProvider.GetUtcNow(), actorId);
        if ((await assignments.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogEnded(logger, actorId, assignment.Id, AccessEndReason.Manual);
        return await DetailAsync(assignment.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> EndAccessForClosedProjectAsync(Guid projectId, Guid actorUserId, CancellationToken cancellationToken)
    {
        IReadOnlyList<AccessRelationship> active = await assignments.FindActiveOnProjectForUpdateAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (active.Count == 0)
        {
            return 0;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (AccessRelationship assignment in active)
        {
            End(assignment, AccessEndReason.ProjectClosed, now, actorUserId);
        }

        SaveResult saved = await assignments.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved.Outcome != SaveOutcome.Saved)
        {
            throw new InvalidOperationException($"The assignments on project {projectId} could not be ended: {saved.Outcome}.");
        }

        foreach (AccessRelationship assignment in active)
        {
            LogEnded(logger, actorUserId, assignment.Id, AccessEndReason.ProjectClosed);
        }

        return active.Count;
    }

    /// <summary>ENDED now, or at its start if it has not started, or at its own end if that has already passed.</summary>
    private static void End(AccessRelationship assignment, AccessEndReason reason, DateTimeOffset now, Guid actorId)
    {
        assignment.EndsAt = assignment.EndsAt is { } endsAt && endsAt < now ? endsAt
            : assignment.StartsAt > now ? assignment.StartsAt
            : now;
        assignment.EndReason = reason;
        assignment.Status = AccessRelationshipStatus.Ended;
        assignment.UpdatedAt = now;
        assignment.UpdatedBy = actorId;
    }

    private async Task<FieldIssue[]> ReferenceIssuesAsync(AccessRelationshipDraft draft, ProjectFacts? project, CancellationToken cancellationToken)
    {
        FieldIssue?[] issues =
        [
            await ReferenceChecks.ActiveDepartmentAsync(departments, draft.DepartmentId, "departmentId", cancellationToken).ConfigureAwait(false),
            await ReferenceChecks.UnretiredEntityAsync(entities, draft.ExternalEntityId, "externalEntityId", cancellationToken).ConfigureAwait(false),
            await ReferenceChecks.ActiveInternalUserAsync(users, draft.SponsorUserId, "sponsorUserId", cancellationToken).ConfigureAwait(false),
            draft.ProjectId is not null && project is null ? new FieldIssue("projectId", FieldIssue.NotFound) : null,
        ];
        return [.. issues.OfType<FieldIssue>()];
    }

    /// <summary>ADR-013 for an external user: every rule the draft breaks, so the administrator sees them all at once.</summary>
    private static FieldIssue[] ExternalGrantIssues(UserDetail user, ProfileVersionFacts version, AccessRelationshipDraft draft, ProjectFacts? project)
    {
        FieldIssue?[] issues =
        [
            version.RoleIsExternalEligible ? null : new FieldIssue("permissionProfileVersionId", FieldIssue.NotExternalEligible),
            draft.ExternalEntityId is { } entity && entity != user.ExternalEntityId ? new FieldIssue("externalEntityId", FieldIssue.OutsideEntity) : null,
            draft.DepartmentId is null ? null : new FieldIssue("departmentId", FieldIssue.NotAllowed),
            draft.SponsorUserId is null ? new FieldIssue("sponsorUserId", FieldIssue.Required) : null,
            version.RoleCode == ProjectManagerRoleCode && draft.ProjectId is null ? new FieldIssue("projectId", FieldIssue.Required) : null,
            project is not null && project.ExternalEntityId != user.ExternalEntityId ? new FieldIssue("projectId", FieldIssue.OutsideEntity) : null,
        ];
        return [.. issues.OfType<FieldIssue>()];
    }

    private async Task<AdministrationResult<AccessRelationshipDetail>> DetailAsync(Guid accessRelationshipId, CancellationToken cancellationToken) =>
        await assignments.FindDetailAsync(accessRelationshipId, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Assignment {accessRelationshipId} was saved and cannot be read back.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Role assignment: {ActorId} created assignment {AccessRelationshipId} of {RoleCode} for user {UserId}.")]
    private static partial void LogAssigned(ILogger logger, Guid actorId, Guid accessRelationshipId, Guid userId, string roleCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role assignment: {ActorId} ended assignment {AccessRelationshipId} ({Reason}).")]
    private static partial void LogEnded(ILogger logger, Guid actorId, Guid accessRelationshipId, AccessEndReason reason);
}
