using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.FollowUp;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// WF-01 registration and activation (TASK-041). Each method that changes a project's state is one edge of
/// <see cref="ProjectLifecycle"/> and checks that edge before it writes; the review outcome edges are
/// <see cref="EventHandlers.ProjectApprovalOutcomeHandler"/>'s. No method changes a state as a side effect of anything else.
/// </summary>
internal sealed class ProjectService(
    IProjectRepository repository,
    ProjectAccess access,
    ProjectReferences references,
    ProjectManagerEligibility managers,
    IApprovalRequests approvals,
    IEnumerable<IProjectActivationPrecondition> activationPreconditions,
    IAuthorizationEngine engine,
    ProjectIndicatorReader indicators,
    IAuditTrail audit,
    TimeProvider timeProvider) : IProjectService
{
    public async Task<ProjectPage> ListAsync(Guid callerId, ProjectQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectView, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return new ProjectPage([], query.Page.Page, query.Page.PageSize, 0);
        }

        (IReadOnlyList<ProjectEntity> items, int total) = await repository.ListAsync(scope, query, cancellationToken).ConfigureAwait(false);
        Func<ProjectEntity, ProjectIndicators> indicatorsOf = await indicators.ForAsync(items, cancellationToken).ConfigureAwait(false);
        return new ProjectPage([.. items.Select(p => ProjectMapping.ToSummary(p, indicatorsOf(p)))], query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ProjectDetail>>> GetAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectEntity? project = await repository.FindAsync(projectId, null, cancellationToken).ConfigureAwait(false);
        return project is null ? AdministrationError.NotFound
            : await access.CheckAsync(callerId, PermissionCatalogue.ProjectView, project, cancellationToken).ConfigureAwait(false) is { } refused ? refused
            : await VersionedAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectDetail>>> CreateAsync(Guid callerId, ProjectDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (await references.CheckAsync(draft, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (await LegacyReferenceTakenAsync(draft, null, cancellationToken).ConfigureAwait(false) is { } taken)
        {
            return taken;
        }

        // ADR-013: an entity user registers a project of their own entity only; the engine's cross-entity isolation decides it. The caller
        // will own the draft (ProjectAccess.OwnerOf), so OWN registers it.
        if (await access.CheckAnchorsAsync(callerId, PermissionCatalogue.ProjectRegister, null, draft, callerId, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectEntity project = new()
        {
            Id = Guid.CreateVersion7(now),
            Title = draft.Title,
            LifecycleState = ProjectLifecycleState.Draft,
            RevisionNo = 1,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        ProjectMapping.Apply(draft, project);
        repository.Add(project);
        audit.Stage(ProjectAudit.Created(callerId, project));
        // A new row with a new id: no conflict is expected, so any is a fault.
        ProjectSaveOutcome outcome = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        return outcome == ProjectSaveOutcome.Saved
            ? await VersionedAsync(project, cancellationToken).ConfigureAwait(false)
            : throw new InvalidOperationException($"Saving a new project failed: {outcome}.");
    }

    public async Task<AdministrationResult<Versioned<ProjectDetail>>> UpdateAsync(
        Guid callerId, Guid projectId, ProjectDraft draft, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.ProjectRegister, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!ProjectLifecycle.IsEditable(project.LifecycleState))
        {
            return NotEditable(project);
        }

        if (await references.CheckAsync(draft, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (await LegacyReferenceTakenAsync(draft, project.Id, cancellationToken).ConfigureAwait(false) is { } taken)
        {
            return taken;
        }

        // Moving a project to another department or entity needs the permission there as well: no one moves a project out
        // of their own reach, or into an entity that is not theirs (ADR-013).
        if ((draft.DepartmentId, draft.ExternalEntityId) != (project.DepartmentId, project.ExternalEntityId)
            && await access.CheckAnchorsAsync(callerId, PermissionCatalogue.ProjectRegister, project.Id, draft, ProjectAccess.OwnerOf(project), cancellationToken).ConfigureAwait(false) is { } moveRefused)
        {
            return moveRefused;
        }

        ProjectDraft before = ProjectMapping.ToDraft(project);
        ProjectMapping.Apply(draft, project);
        Touch(project, callerId);
        audit.Stage(ProjectAudit.Changed(callerId, before, project));
        return await SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        // Client review, item 4: any state, and everything stays in the database.
        if (await access.HoldsAsync(callerId, PermissionCatalogue.ProjectDelete, project, cancellationToken).ConfigureAwait(false))
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            project.DeletedAt = now;
            project.DeletedBy = callerId;
            project.UpdatedAt = now;
            project.UpdatedBy = callerId;
        }
        else
        {
            if (await access.CheckAsync(callerId, PermissionCatalogue.ProjectRegister, project, cancellationToken).ConfigureAwait(false) is { } refused)
            {
                return refused;
            }

            if (project.LifecycleState != ProjectLifecycleState.Draft)
            {
                return NotEditable(project);
            }

            // HARD_DRAFT (ERD §5.4): by its owner, the person who created the draft.
            if (project.CreatedBy != callerId)
            {
                return AdministrationError.Forbidden;
            }

            repository.Remove(project);
        }

        audit.Stage(ProjectAudit.Deleted(callerId, project));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ProjectSaveOutcome.Saved => null,
            ProjectSaveOutcome.InUse => AdministrationError.Conflict(ProjectErrorCodes.InUse),
            ProjectSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<ProjectDetail>>> SubmitAsync(
        Guid callerId, Guid projectId, ProjectSubmission submission, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.ProjectRegister, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!ProjectLifecycle.Allows(project.LifecycleState, ProjectLifecycleState.Submitted))
        {
            return TransitionRefused(project);
        }

        // The draft is checked again: an item retired or an entity suspended since it was written cannot go to review.
        if (await references.CheckAsync(ProjectMapping.ToDraft(project), cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (Incomplete(project) is { } incomplete)
        {
            return incomplete;
        }

        if (!await managers.IsEligibleAsync(submission.ProjectManagerUserId, project, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Rule(ProjectErrorCodes.ManagerInvalid, new FieldIssue("projectManagerUserId", FieldIssue.NotAllowed));
        }

        ProjectLifecycleState from = project.LifecycleState;
        int fromRevisionNo = project.RevisionNo;
        Guid? fromManagerId = project.ProjectManagerUserId;

        // A RETURNED registration comes back as the next revision, which the next review routes as a new WF-11 run (TASK-035 D-9).
        if (from == ProjectLifecycleState.Returned)
        {
            project.RevisionNo++;
        }

        project.LifecycleState = ProjectLifecycleState.Submitted;
        project.ProjectManagerUserId = submission.ProjectManagerUserId;
        project.SubmittedByUserId = callerId;
        Touch(project, callerId);
        audit.Stage(ProjectAudit.Submitted(callerId, project, from, fromRevisionNo, fromManagerId));
        return await SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectDetail>>> WithdrawAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.ProjectRegister, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (project.LifecycleState != ProjectLifecycleState.Submitted)
        {
            return TransitionRefused(project);
        }

        // ERD: the Project Manager is absent while DRAFT; the next submission names one again.
        Guid? fromManagerId = project.ProjectManagerUserId;
        project.LifecycleState = ProjectLifecycleState.Draft;
        project.ProjectManagerUserId = null;
        project.SubmittedByUserId = null;
        Touch(project, callerId);
        audit.Stage(ProjectAudit.Withdrawn(callerId, project, fromManagerId));
        return await SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectDetail>>> StartReviewAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckGateAsync(callerId, PermissionCatalogue.ProjectReview, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!ProjectLifecycle.Allows(project.LifecycleState, ProjectLifecycleState.UnderReview))
        {
            return TransitionRefused(project);
        }

        // The run is staged, not saved: it commits with UNDER_REVIEW in the save below, or not at all (M-11). The requester is
        // whoever submitted the registration, not the reviewer starting it, or the reviewer could not decide their own review.
        AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
            new ApprovalStart(
                new ApprovalSubject(ProjectApprovalRouting.SubjectModule, ProjectApprovalRouting.SubjectType, project.Id, project.RevisionNo),
                ProjectApprovalRouting.RegistrationRoutingKey,
                project.SubmittedByUserId ?? callerId,
                project.Id,
                project.DepartmentId,
                project.GovernanceProfileItemId,
                null,
                project.RegistrationBudgetSar),
            cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded)
        {
            return run.Error;
        }

        project.LifecycleState = ProjectLifecycleState.UnderReview;
        Touch(project, callerId);
        audit.Stage(ProjectAudit.ReviewStarted(callerId, project, run.Value.Id));
        return await SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ProjectDetail>>> ActivateAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckGateAsync(callerId, PermissionCatalogue.ProjectActivate, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!ProjectLifecycle.Allows(project.LifecycleState, ProjectLifecycleState.Active))
        {
            return TransitionRefused(project);
        }

        // ADR-009: what other modules require before a project goes Active, WF-03's ACTIVE baseline first among them.
        ProjectFacts facts = ProjectFactsReader.FactsOf(project);
        foreach (IProjectActivationPrecondition precondition in activationPreconditions)
        {
            if (await precondition.CheckAsync(facts, cancellationToken).ConfigureAwait(false) is { } unmet)
            {
                return unmet;
            }
        }

        project.LifecycleState = ProjectLifecycleState.Active;
        project.ActivatedAt = timeProvider.GetUtcNow();
        Touch(project, callerId);
        audit.Stage(ProjectAudit.Activated(callerId, project));
        return await SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdministrationError?> LegacyReferenceTakenAsync(ProjectDraft draft, Guid? projectId, CancellationToken cancellationToken) =>
        draft.LegacyReference is { } reference
        && (await repository.ListByLegacyReferencesAsync([reference], cancellationToken).ConfigureAwait(false)).TryGetValue(reference, out ProjectEntity? holder)
        && holder.Id != projectId
            ? AdministrationError.Conflict(ProjectErrorCodes.LegacyReferenceTaken, new FieldIssue("legacyReference", FieldIssue.Duplicate))
            : null;

    /// <summary>Review needs what the governance profile is assigned on (TASK-105): the budget and the planned duration.</summary>
    private static AdministrationError? Incomplete(ProjectEntity project)
    {
        List<FieldIssue> missing = [];
        if (project.RegistrationBudgetSar is null)
        {
            missing.Add(new FieldIssue("registrationBudgetSar", FieldIssue.Required));
        }

        if (project.PlannedStartDate is null)
        {
            missing.Add(new FieldIssue("plannedStartDate", FieldIssue.Required));
        }

        if (project.PlannedEndDate is null)
        {
            missing.Add(new FieldIssue("plannedEndDate", FieldIssue.Required));
        }

        return missing.Count == 0 ? null : AdministrationError.Rule(ProjectErrorCodes.Incomplete, [.. missing]);
    }

    /// <summary>A command not allowed from the project's state; CLOSED takes no write at all (api-conventions §4.4).</summary>
    private static AdministrationError TransitionRefused(ProjectEntity project) =>
        project.LifecycleState == ProjectLifecycleState.Closed ? AdministrationError.TerminalState : AdministrationError.InvalidTransition;

    private static AdministrationError NotEditable(ProjectEntity project) =>
        project.LifecycleState == ProjectLifecycleState.Closed ? AdministrationError.TerminalState : AdministrationError.Conflict(ProjectErrorCodes.NotEditable);

    private async Task<AdministrationResult<Versioned<ProjectDetail>>> SaveAsync(ProjectEntity project, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ProjectSaveOutcome.Saved
            ? await VersionedAsync(project, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    private async Task<Versioned<ProjectDetail>> VersionedAsync(ProjectEntity project, CancellationToken cancellationToken) =>
        new(ProjectMapping.ToDetail(project, await indicators.OfAsync(project, cancellationToken).ConfigureAwait(false)), repository.RowVersionOf(project));

    private void Touch(ProjectEntity project, Guid by)
    {
        project.UpdatedAt = timeProvider.GetUtcNow();
        project.UpdatedBy = by;
    }
}
