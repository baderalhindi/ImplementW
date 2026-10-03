using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Domain.Milestone;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Milestone;

/// <summary>
/// WF-05's achievement claims (TASK-050). The milestone a claim names is WF-03's row, read through
/// <see cref="IProjectMilestoneReader"/> (edge 10) and never copied. A revision is opened DRAFT, evidenced through
/// DocumentManagement on the revision itself (edge 16), and submitted to WF-11 (edge 25); acceptance is applied by
/// <see cref="EventHandlers.MilestoneAchievementOutcomeHandler"/>. Nothing here changes a revision once it is submitted.
/// </summary>
internal sealed class MilestoneAchievementService(
    IMilestoneRepository repository,
    IProjectMilestoneReader milestones,
    IProjectFactsReader projects,
    MilestoneAccess access,
    MilestoneEvidencePolicy evidencePolicy,
    IDocumentLinks documents,
    IApprovalRequests approvals,
    IAuditTrail audit,
    TimeProvider timeProvider) : IMilestoneAchievementService
{
    public async Task<MilestoneAchievementPage> ListAsync(Guid callerId, MilestoneAchievementQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        Guid? projectId = query.ProjectMilestoneId is { } milestoneId
            ? (await milestones.FindAsync(milestoneId, cancellationToken).ConfigureAwait(false))?.ProjectId
            : query.ProjectId;
        if (!(projectId is { } id
              && await projects.FindAsync(id, cancellationToken).ConfigureAwait(false) is { } project
              && await access.CanViewAsync(callerId, project, cancellationToken).ConfigureAwait(false)))
        {
            return new MilestoneAchievementPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<MilestoneAchievement> items, int total) =
            await repository.PageAsync(query.ProjectMilestoneId, query.ProjectMilestoneId is null ? projectId : null, page, cancellationToken).ConfigureAwait(false);
        return new MilestoneAchievementPage([.. items.Select(MilestoneMapping.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> GetAsync(Guid callerId, Guid achievementId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.MilestoneView, achievementId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : Versioned(loaded.Achievement!);
    }

    public async Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> CreateAsync(Guid callerId, MilestoneAchievementDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // A milestone the caller cannot see is one that does not exist (R-47).
        ProjectMilestoneFacts? milestone = await milestones.FindAsync(draft.ProjectMilestoneId, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = milestone is null ? null : await projects.FindAsync(milestone.ProjectId, cancellationToken).ConfigureAwait(false);
        AdministrationError? refused = project is null
            ? AdministrationError.NotFound
            : await access.CheckAsync(callerId, PermissionCatalogue.MilestoneSubmit, project, cancellationToken).ConfigureAwait(false);
        if (refused is { Kind: AdministrationErrorKind.NotFound })
        {
            return AdministrationError.Rule(MilestoneErrorCodes.NotClaimable, new FieldIssue("projectMilestoneId", FieldIssue.NotFound));
        }

        if (refused is not null)
        {
            return refused;
        }

        if (ClaimRefused(project!, milestone!, draft.ClaimedAchievementDate) is { } notClaimable)
        {
            return notClaimable;
        }

        IReadOnlyList<MilestoneAchievement> revisions = await repository.ListRevisionsAsync(milestone!.Id, cancellationToken).ConfigureAwait(false);
        if (revisions.Any(r => MilestoneAchievementWorkflow.IsOpen(r.Status)))
        {
            return AdministrationError.Conflict(MilestoneErrorCodes.AchievementOpen);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        MilestoneAchievement achievement = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectMilestoneId = milestone.Id,
            ProjectId = project!.Id,
            RevisionNo = revisions.Count == 0 ? 1 : revisions.Max(r => r.RevisionNo) + 1,
            Status = MilestoneAchievementStatus.Draft,
            ClaimedAchievementDate = draft.ClaimedAchievementDate,
            Narrative = draft.Narrative,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(achievement);

        // A revision opened while one is ACCEPTED is a correction of it: the accepted one stays current until this one is accepted.
        Guid? corrects = revisions.SingleOrDefault(r => r.Status == MilestoneAchievementStatus.Accepted)?.Id;
        audit.Stage(MilestoneAudit.Started(callerId, project, achievement, corrects));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            MilestoneSaveOutcome.Saved => Versioned(achievement),
            MilestoneSaveOutcome.Duplicate => AdministrationError.Conflict(MilestoneErrorCodes.AchievementOpen),
            MilestoneSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> UpdateAsync(
        Guid callerId, Guid achievementId, MilestoneAchievementChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.MilestoneSubmit, achievementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, MilestoneAchievement achievement) = (loaded.Project!, loaded.Achievement!);
        if (achievement.Status != MilestoneAchievementStatus.Draft)
        {
            return AdministrationError.Conflict(MilestoneErrorCodes.AchievementNotEditable);
        }

        if (ClaimRefused(project, await MilestoneOfAsync(achievement, cancellationToken).ConfigureAwait(false), changes.ClaimedAchievementDate) is { } notClaimable)
        {
            return notClaimable;
        }

        ClaimInputs before = ClaimInputs.Of(achievement);
        achievement.ClaimedAchievementDate = changes.ClaimedAchievementDate;
        achievement.Narrative = changes.Narrative;
        Touch(achievement, callerId);
        audit.Stage(MilestoneAudit.Changed(callerId, project, before, achievement));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == MilestoneSaveOutcome.Saved
            ? Versioned(achievement)
            : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid achievementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.MilestoneSubmit, achievementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, MilestoneAchievement achievement) = (loaded.Project!, loaded.Achievement!);
        if (achievement.Status != MilestoneAchievementStatus.Draft)
        {
            return AdministrationError.Conflict(MilestoneErrorCodes.AchievementNotEditable);
        }

        // The draft's links end with it, so no document stays attached to a revision that is gone; the documents remain.
        await using IMilestoneWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        foreach (BusinessLinkDetail link in (await documents.FindLinksAsync(TargetOf(achievement), cancellationToken).ConfigureAwait(false)).Where(l => l.UnlinkedAt is null))
        {
            AdministrationResult<BusinessLinkDetail> unlinked = await documents.UnlinkAsync(callerId, link.Id, cancellationToken).ConfigureAwait(false);
            if (!unlinked.Succeeded)
            {
                return unlinked.Error;
            }
        }

        audit.Stage(MilestoneAudit.Deleted(callerId, project, achievement));
        repository.Remove(achievement);
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != MilestoneSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    public async Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> SubmitAsync(Guid callerId, Guid achievementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.MilestoneSubmit, achievementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, MilestoneAchievement achievement) = (loaded.Project!, loaded.Achievement!);
        if (achievement.Status != MilestoneAchievementStatus.Draft)
        {
            return AdministrationError.InvalidTransition;
        }

        ProjectMilestoneFacts milestone = await MilestoneOfAsync(achievement, cancellationToken).ConfigureAwait(false);
        if (ClaimRefused(project, milestone, achievement.ClaimedAchievementDate) is { } notClaimable)
        {
            return notClaimable;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        EvidenceRequirement requirement = await evidencePolicy.RequirementAsync(milestone.MilestoneCategoryItemId, now, cancellationToken).ConfigureAwait(false);
        IReadOnlySet<Guid> satisfied = await documents.GetSatisfiedEvidenceTypesAsync(TargetOf(achievement), cancellationToken).ConfigureAwait(false);
        if (requirement.MissingFrom(satisfied).Count > 0)
        {
            return AdministrationError.Rule(MilestoneErrorCodes.EvidenceRequired, new FieldIssue("evidence", FieldIssue.Required));
        }

        // The run is staged, not saved: it commits with SUBMITTED in the save below, or not at all (M-11). The requester is the
        // claimant, so WF-11's own rule keeps them from accepting it, and WF-11 gives no external user authority (ADR-013).
        AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
            new ApprovalStart(
                new ApprovalSubject(MilestoneApprovalRouting.SubjectModule, MilestoneApprovalRouting.SubjectType, achievement.Id, achievement.RevisionNo),
                MilestoneApprovalRouting.AchievementRoutingKey,
                callerId,
                project.Id,
                project.DepartmentId,
                project.GovernanceProfileItemId,
                null,
                null),
            cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded)
        {
            return run.Error;
        }

        achievement.Status = MilestoneAchievementStatus.Submitted;
        achievement.SubmittedByUserId = callerId;
        achievement.SubmittedAt = now;
        Touch(achievement, callerId);
        audit.Stage(MilestoneAudit.Submitted(callerId, project, achievement, run.Value.Id, requirement.PolicyVersionId));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == MilestoneSaveOutcome.Saved
            ? Versioned(achievement)
            : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<MilestoneEvidenceDetail>> GetEvidenceAsync(Guid callerId, Guid achievementId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.MilestoneView, achievementId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        MilestoneAchievement achievement = loaded.Achievement!;
        ProjectMilestoneFacts milestone = await MilestoneOfAsync(achievement, cancellationToken).ConfigureAwait(false);
        EvidenceRequirement requirement = await evidencePolicy.RequirementAsync(milestone.MilestoneCategoryItemId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        BusinessTarget target = TargetOf(achievement);
        IReadOnlySet<Guid> satisfied = await documents.GetSatisfiedEvidenceTypesAsync(target, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<BusinessLinkDetail> links = await documents.FindLinksAsync(target, cancellationToken).ConfigureAwait(false);
        return new MilestoneEvidenceDetail(achievement.Id, requirement.PolicyVersionId, requirement.Mandatory, [.. satisfied.Order()], links);
    }

    public async Task<AdministrationResult<EvidenceReferenceDetail>> AttachEvidenceAsync(
        Guid callerId, Guid achievementId, MilestoneEvidenceAttachment attachment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.MilestoneSubmit, achievementId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, MilestoneAchievement achievement) = (loaded.Project!, loaded.Achievement!);
        if (EvidenceRefused(project, achievement) is { } closed)
        {
            return closed;
        }

        // Linking and pinning commit together: a version DocumentManagement refuses to pin leaves no link behind.
        await using IMilestoneWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        AdministrationResult<BusinessLinkDetail> link = await documents.LinkAsync(callerId, attachment.DocumentId, TargetOf(achievement), BusinessLinkRole.Attachment, cancellationToken)
            .ConfigureAwait(false);
        if (!link.Succeeded)
        {
            return link.Error;
        }

        AdministrationResult<EvidenceReferenceDetail> evidence = await documents.DesignateEvidenceAsync(
            callerId, new EvidenceDesignation(link.Value.Id, attachment.DocumentVersionId, attachment.EvidenceTypeItemId), cancellationToken).ConfigureAwait(false);
        return evidence.Succeeded
            ? await EvidenceChangedAsync(work, achievement, callerId, MilestoneAudit.EvidenceAttached(callerId, project, achievement, evidence.Value), evidence.Value, cancellationToken)
                .ConfigureAwait(false)
            : evidence.Error;
    }

    public async Task<AdministrationResult<EvidenceReferenceDetail>> WithdrawEvidenceAsync(
        Guid callerId, Guid achievementId, Guid evidenceReferenceId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.MilestoneSubmit, achievementId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, MilestoneAchievement achievement) = (loaded.Project!, loaded.Achievement!);
        if (EvidenceRefused(project, achievement) is { } closed)
        {
            return closed;
        }

        // Only this revision's evidence is withdrawn here; another record's is that record's (R-47).
        IReadOnlyList<BusinessLinkDetail> links = await documents.FindLinksAsync(TargetOf(achievement), cancellationToken).ConfigureAwait(false);
        if (!links.SelectMany(l => l.Evidence).Any(e => e.Id == evidenceReferenceId))
        {
            return AdministrationError.NotFound;
        }

        await using IMilestoneWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        AdministrationResult<EvidenceReferenceDetail> withdrawn = await documents.WithdrawEvidenceAsync(callerId, evidenceReferenceId, cancellationToken).ConfigureAwait(false);
        return withdrawn.Succeeded
            ? await EvidenceChangedAsync(work, achievement, callerId, MilestoneAudit.EvidenceWithdrawn(callerId, project, achievement, withdrawn.Value), withdrawn.Value, cancellationToken)
                .ConfigureAwait(false)
            : withdrawn.Error;
    }

    /// <summary>
    /// The revision's evidence changed: touching the revision's row makes a submission that read the evidence before the change
    /// fail on its stale version (R-21) instead of submitting evidence that is no longer there.
    /// </summary>
    private async Task<AdministrationResult<EvidenceReferenceDetail>> EvidenceChangedAsync(
        IMilestoneWork work, MilestoneAchievement achievement, Guid callerId, AuditEntry entry, EvidenceReferenceDetail evidence, CancellationToken cancellationToken)
    {
        Touch(achievement, callerId);
        audit.Stage(entry);
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != MilestoneSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    /// <summary>
    /// The first reason the claim cannot be made now: the project is not ACTIVE, the milestone is CANCELLED in the schedule, or
    /// the date is after today (UTC). An ACHIEVED milestone can be claimed: that claim is a correction.
    /// </summary>
    private AdministrationError? ClaimRefused(ProjectFacts project, ProjectMilestoneFacts milestone, DateOnly claimedAchievementDate) =>
        project.Status != ProjectLifecycleState.Active ? AdministrationError.Rule(MilestoneErrorCodes.ProjectNotActive)
        : milestone.Status == ProjectMilestoneStatus.Cancelled ? AdministrationError.Rule(MilestoneErrorCodes.NotClaimable, new FieldIssue("projectMilestoneId", FieldIssue.NotAllowed))
        : claimedAchievementDate > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)
            ? AdministrationError.Rule(MilestoneErrorCodes.ClaimedDateInvalid, new FieldIssue("claimedAchievementDate", FieldIssue.NotAllowed))
        : null;

    private static AdministrationError? EvidenceRefused(ProjectFacts project, MilestoneAchievement achievement) =>
        achievement.Status != MilestoneAchievementStatus.Draft ? AdministrationError.Conflict(MilestoneErrorCodes.AchievementNotEditable)
        : project.Status != ProjectLifecycleState.Active ? AdministrationError.Rule(MilestoneErrorCodes.ProjectNotActive)
        : null;

    /// <summary>The revision's milestone: a revision always names one, by a foreign key to the shared row.</summary>
    private async Task<ProjectMilestoneFacts> MilestoneOfAsync(MilestoneAchievement achievement, CancellationToken cancellationToken) =>
        await milestones.FindAsync(achievement.ProjectMilestoneId, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Achievement {achievement.Id} names no milestone.");

    /// <summary>The record DocumentManagement links the revision's evidence to: the revision, not the milestone, so each revision keeps its own.</summary>
    internal static BusinessTarget TargetOf(MilestoneAchievement achievement) =>
        new(MilestoneApprovalRouting.SubjectModule, MilestoneApprovalRouting.SubjectType, achievement.Id);

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid achievementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        MilestoneAchievement? achievement = await repository.FindAsync(achievementId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = achievement is null ? null : await projects.FindAsync(achievement.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, achievement, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    private Versioned<MilestoneAchievementDetail> Versioned(MilestoneAchievement achievement) => new(MilestoneMapping.ToDetail(achievement), repository.RowVersionOf(achievement));

    private void Touch(MilestoneAchievement achievement, Guid by)
    {
        achievement.UpdatedAt = timeProvider.GetUtcNow();
        achievement.UpdatedBy = by;
    }

    private sealed record Loaded(ProjectFacts? Project, MilestoneAchievement? Achievement, AdministrationError? Error);
}
