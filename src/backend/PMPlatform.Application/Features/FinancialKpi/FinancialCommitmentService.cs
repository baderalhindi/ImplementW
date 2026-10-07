using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// The Approved Budget's versions (TASK-052, ADR-008). Every request writes an APPROVED_BUDGET version in SAR at project-total
/// level with MANUAL provenance — the caller entered it — unless the field is INTEGRATED, when none is written by hand. Each
/// version needs a referenced document, held as CLEAN evidence through DocumentManagement (edge 38), before WF-11 sees it (edge
/// 26). Nothing here changes a version once it is submitted; WF-11's outcome is applied by
/// <see cref="EventHandlers.FinancialKpiApprovalOutcomeHandler"/>.
/// </summary>
internal sealed class FinancialCommitmentService(
    IFinancialKpiRepository repository,
    IProjectFactsReader projects,
    FinancialKpiAccess access,
    IDocumentLinks documents,
    IApprovalRequests approvals,
    CommitmentChangeAuthorization changes,
    IAuditTrail audit,
    TimeProvider timeProvider) : IFinancialCommitmentService
{
    public async Task<FinancialCommitmentPage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not { } project
            || !await access.CanAsync(callerId, PermissionCatalogue.FinancialView, project, cancellationToken).ConfigureAwait(false))
        {
            return new FinancialCommitmentPage([], page.Page, page.PageSize, 0);
        }

        FieldMask mask = await access.MaskAsync(callerId, PermissionCatalogue.FinancialView, FinancialKpiMasking.Commitment, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<FinancialCommitment> items, int total) = await repository.PageCommitmentsAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new FinancialCommitmentPage([.. items.Select(c => FinancialKpiMapping.ToDetail(c, mask))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> GetAsync(Guid callerId, Guid commitmentId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialView, commitmentId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(callerId, loaded.Commitment!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> CreateAsync(Guid callerId, FinancialCommitmentDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ProjectFacts? project = await projects.FindAsync(draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if ((project is null ? AdministrationError.NotFound
                : await access.CheckAsync(callerId, PermissionCatalogue.FinancialSubmit, project, cancellationToken).ConfigureAwait(false)) is { } refused)
        {
            return refused;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await FiguresRefusedAsync(project!, draft.AmountSar, draft.AsOfDate, now, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        IReadOnlyList<FinancialCommitment> versions = await repository.ListCommitmentsAsync(project!.Id, CommitmentType.ApprovedBudget, cancellationToken).ConfigureAwait(false);
        if (versions.Any(v => ApprovedVersionWorkflow.IsOpen(v.Status)))
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.CommitmentOpen);
        }

        FinancialCommitment commitment = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            CommitmentType = CommitmentType.ApprovedBudget,
            VersionNo = versions.Count == 0 ? 1 : versions.Max(v => v.VersionNo) + 1,
            Status = ApprovedVersionStatus.Draft,
            AmountSar = draft.AmountSar,
            EffectiveFrom = draft.EffectiveFrom,
            SourceType = FinancialSourceType.Manual,
            SourceReference = draft.SourceReference,
            AsOfDate = draft.AsOfDate,
            EnteredByUserId = callerId,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(commitment);
        audit.Stage(FinancialKpiAudit.VersionCreated(callerId, project, VersionFacts.Of(commitment)));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => await VersionedAsync(callerId, commitment, cancellationToken).ConfigureAwait(false),
            FinancialKpiSaveOutcome.Duplicate => AdministrationError.Conflict(FinancialKpiErrorCodes.CommitmentOpen),
            FinancialKpiSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> UpdateAsync(
        Guid callerId, Guid commitmentId, FinancialCommitmentChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, commitmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialCommitment commitment) = (loaded.Project!, loaded.Commitment!);
        if (!ApprovedVersionWorkflow.IsEditable(commitment.Status))
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.VersionNotEditable);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await FiguresRefusedAsync(project, changes.AmountSar, changes.AsOfDate, now, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        VersionFacts before = VersionFacts.Of(commitment);
        commitment.AmountSar = changes.AmountSar;
        commitment.EffectiveFrom = changes.EffectiveFrom;
        commitment.SourceReference = changes.SourceReference;
        commitment.AsOfDate = changes.AsOfDate;
        commitment.EnteredByUserId = callerId;
        Touch(commitment, callerId, now);
        audit.Stage(FinancialKpiAudit.VersionChanged(callerId, project, before, VersionFacts.Of(commitment)));
        return await SaveAsync(callerId, commitment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid commitmentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, commitmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialCommitment commitment) = (loaded.Project!, loaded.Commitment!);
        if (commitment.Status != ApprovedVersionStatus.Draft)
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.VersionNotEditable);
        }

        // The draft's links end with it, so no document stays attached to a version that is gone; the documents remain.
        await using IFinancialKpiWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        foreach (BusinessLinkDetail link in (await documents.FindLinksAsync(TargetOf(commitment), cancellationToken).ConfigureAwait(false)).Where(l => l.UnlinkedAt is null))
        {
            AdministrationResult<BusinessLinkDetail> unlinked = await documents.UnlinkAsync(callerId, link.Id, cancellationToken).ConfigureAwait(false);
            if (!unlinked.Succeeded)
            {
                return unlinked.Error;
            }
        }

        audit.Stage(FinancialKpiAudit.VersionDeleted(callerId, project, VersionFacts.Of(commitment)));
        repository.Remove(commitment);
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != FinancialKpiSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    public async Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> SubmitAsync(
        Guid callerId, Guid commitmentId, CommitmentSubmission submission, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, commitmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialCommitment commitment) = (loaded.Project!, loaded.Commitment!);
        if (!ApprovedVersionWorkflow.Allows(commitment.Status, ApprovedVersionStatus.Submitted))
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await FiguresRefusedAsync(project, commitment.AmountSar, commitment.AsOfDate, now, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        // ADR-008 gate: any change to the Approved Budget requires a referenced document; periodic actuals and forecast do not.
        if ((await documents.GetSatisfiedEvidenceTypesAsync(TargetOf(commitment), cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            return AdministrationError.Rule(FinancialKpiErrorCodes.BudgetDocumentRequired, new FieldIssue("documents", FieldIssue.Required));
        }

        // A change to an ACTIVE Approved Budget implements an approved WF-08 change (edge 12), applied when WF-11's approval makes it
        // take effect; the project's first Approved Budget is no change.
        if (await repository.FindActiveCommitmentAsync(project.Id, CommitmentType.ApprovedBudget, track: false, cancellationToken).ConfigureAwait(false) is { } active)
        {
            if (submission.ChangeAuthorizationId is not { } authorizationId)
            {
                return AdministrationError.Rule(FinancialKpiErrorCodes.ChangeAuthorizationRequired, new FieldIssue("changeAuthorizationId", FieldIssue.Required));
            }

            if (!await changes.IsApplicableAsync(project, active, authorizationId, cancellationToken).ConfigureAwait(false))
            {
                return AdministrationError.Rule(FinancialKpiErrorCodes.ChangeAuthorizationRequired, new FieldIssue("changeAuthorizationId", FieldIssue.NotAllowed));
            }

            commitment.ChangeAuthorizationId = authorizationId;
        }

        // A RETURNED version comes back as the next revision, which a new WF-11 run reviews (TASK-035 D-9).
        ApprovedVersionStatus from = commitment.Status;
        if (from == ApprovedVersionStatus.Returned)
        {
            commitment.RevisionNo++;
        }

        // The run is staged, not saved: it commits with SUBMITTED in the save below, or not at all (M-11). The requester is the
        // person who entered the figure, so WF-11's own rule keeps them from approving it.
        AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
            new ApprovalStart(
                new ApprovalSubject(FinancialKpiApprovalRouting.SubjectModule, FinancialKpiApprovalRouting.CommitmentType, commitment.Id, commitment.RevisionNo),
                FinancialKpiApprovalRouting.CommitmentRoutingKey,
                callerId,
                project.Id,
                project.DepartmentId,
                project.GovernanceProfileItemId,
                null,
                commitment.AmountSar),
            cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded)
        {
            return run.Error;
        }

        commitment.Status = ApprovedVersionStatus.Submitted;
        Touch(commitment, callerId, now);
        audit.Stage(FinancialKpiAudit.VersionSubmitted(callerId, project, VersionFacts.Of(commitment), from, run.Value.Id));
        return await SaveAsync(callerId, commitment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<CommitmentDocumentDetail>> GetDocumentsAsync(Guid callerId, Guid commitmentId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialView, commitmentId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        BusinessTarget target = TargetOf(loaded.Commitment!);
        IReadOnlySet<Guid> satisfied = await documents.GetSatisfiedEvidenceTypesAsync(target, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<BusinessLinkDetail> links = await documents.FindLinksAsync(target, cancellationToken).ConfigureAwait(false);
        return new CommitmentDocumentDetail(commitmentId, [.. satisfied.Order()], links);
    }

    public async Task<AdministrationResult<EvidenceReferenceDetail>> AttachDocumentAsync(
        Guid callerId, Guid commitmentId, CommitmentDocumentAttachment attachment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, commitmentId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialCommitment commitment) = (loaded.Project!, loaded.Commitment!);
        if (!ApprovedVersionWorkflow.IsEditable(commitment.Status))
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.VersionNotEditable);
        }

        // Linking and pinning commit together: a version DocumentManagement refuses to pin leaves no link behind.
        await using IFinancialKpiWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        AdministrationResult<BusinessLinkDetail> link = await documents.LinkAsync(callerId, attachment.DocumentId, TargetOf(commitment), BusinessLinkRole.Attachment, cancellationToken)
            .ConfigureAwait(false);
        if (!link.Succeeded)
        {
            return link.Error;
        }

        AdministrationResult<EvidenceReferenceDetail> evidence = await documents.DesignateEvidenceAsync(
            callerId, new EvidenceDesignation(link.Value.Id, attachment.DocumentVersionId, attachment.EvidenceTypeItemId), cancellationToken).ConfigureAwait(false);
        return evidence.Succeeded
            ? await DocumentsChangedAsync(work, commitment, callerId, FinancialKpiAudit.DocumentAttached(callerId, project, commitment, evidence.Value), evidence.Value, cancellationToken)
                .ConfigureAwait(false)
            : evidence.Error;
    }

    public async Task<AdministrationResult<EvidenceReferenceDetail>> WithdrawDocumentAsync(Guid callerId, Guid commitmentId, Guid evidenceReferenceId, CancellationToken cancellationToken)
    {
        Loaded loaded = await LoadAsync(callerId, PermissionCatalogue.FinancialSubmit, commitmentId, null, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, FinancialCommitment commitment) = (loaded.Project!, loaded.Commitment!);
        if (!ApprovedVersionWorkflow.IsEditable(commitment.Status))
        {
            return AdministrationError.Conflict(FinancialKpiErrorCodes.VersionNotEditable);
        }

        // Only this version's documents are withdrawn here; another record's are that record's (R-47).
        IReadOnlyList<BusinessLinkDetail> links = await documents.FindLinksAsync(TargetOf(commitment), cancellationToken).ConfigureAwait(false);
        if (!links.SelectMany(l => l.Evidence).Any(e => e.Id == evidenceReferenceId))
        {
            return AdministrationError.NotFound;
        }

        await using IFinancialKpiWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        AdministrationResult<EvidenceReferenceDetail> withdrawn = await documents.WithdrawEvidenceAsync(callerId, evidenceReferenceId, cancellationToken).ConfigureAwait(false);
        return withdrawn.Succeeded
            ? await DocumentsChangedAsync(work, commitment, callerId, FinancialKpiAudit.DocumentWithdrawn(callerId, project, commitment, withdrawn.Value), withdrawn.Value, cancellationToken)
                .ConfigureAwait(false)
            : withdrawn.Error;
    }

    /// <summary>The record DocumentManagement links a version's referenced documents to: the version itself, so each keeps its own.</summary>
    internal static BusinessTarget TargetOf(FinancialCommitment commitment) =>
        new(FinancialKpiApprovalRouting.SubjectModule, FinancialKpiApprovalRouting.CommitmentType, commitment.Id);

    /// <summary>
    /// Touching the version's row makes a submission that read the documents before the change fail on its stale version (R-21)
    /// instead of submitting without the document it saw.
    /// </summary>
    private async Task<AdministrationResult<EvidenceReferenceDetail>> DocumentsChangedAsync(
        IFinancialKpiWork work, FinancialCommitment commitment, Guid callerId, AuditEntry entry, EvidenceReferenceDetail evidence, CancellationToken cancellationToken)
    {
        Touch(commitment, callerId, timeProvider.GetUtcNow());
        audit.Stage(entry);
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != FinancialKpiSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    /// <summary>The project admits a budget; the amount is a non-negative SAR total; the as-of date has happened; the field is not INTEGRATED.</summary>
    private async Task<AdministrationError?> FiguresRefusedAsync(ProjectFacts project, Money amount, DateOnly asOfDate, DateTimeOffset now, CancellationToken cancellationToken) =>
        ProjectEligibility.PlanningRefused(project)
        ?? (amount.Amount < 0 ? AdministrationError.Rule(FinancialKpiErrorCodes.ValueStatusInvalid, new FieldIssue("amountSar", FieldIssue.NotAllowed)) : null)
        ?? ValueRules.AsOfRefused(asOfDate, ValueRules.Today(now))
        ?? await FinancialSourceModeService.ManualEntryRefusedAsync(repository, project.Id, [(FinancialField.ApprovedBudget, "amountSar")], cancellationToken).ConfigureAwait(false);

    private async Task<Loaded> LoadAsync(Guid callerId, string permissionCode, Guid commitmentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        FinancialCommitment? commitment = await repository.FindCommitmentAsync(commitmentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = commitment is null ? null : await projects.FindAsync(commitment.ProjectId, cancellationToken).ConfigureAwait(false);
        return project is null
            ? new Loaded(null, null, AdministrationError.NotFound)
            : new Loaded(project, commitment, await access.CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false));
    }

    private async Task<AdministrationResult<Versioned<FinancialCommitmentDetail>>> SaveAsync(Guid callerId, FinancialCommitment commitment, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == FinancialKpiSaveOutcome.Saved
            ? await VersionedAsync(callerId, commitment, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    private async Task<Versioned<FinancialCommitmentDetail>> VersionedAsync(Guid callerId, FinancialCommitment commitment, CancellationToken cancellationToken)
    {
        FieldMask mask = await access.MaskAsync(callerId, PermissionCatalogue.FinancialView, FinancialKpiMasking.Commitment, cancellationToken).ConfigureAwait(false);
        return new Versioned<FinancialCommitmentDetail>(FinancialKpiMapping.ToDetail(commitment, mask), repository.RowVersionOf(commitment));
    }

    private static void Touch(FinancialCommitment commitment, Guid by, DateTimeOffset at)
    {
        commitment.UpdatedAt = at;
        commitment.UpdatedBy = by;
    }

    private sealed record Loaded(ProjectFacts? Project, FinancialCommitment? Commitment, AdministrationError? Error);
}
