using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// WF-06's register (TASK-055): risks registered and edited by the project's people, and their history read back — assessment
/// versions with the ratings they recorded, and acceptances. A risk's representation names the issues raised from it. A risk's state moves only through
/// <see cref="RiskLifecycleService"/>.
/// </summary>
internal sealed class RiskService(
    IRiskRepository repository, RiskGate gate, RiskReferences references, RiskViews views, IAuditTrail audit, TimeProvider timeProvider)
    : IRiskService
{
    public async Task<RiskPage> ListRisksAsync(Guid callerId, RiskQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new RiskPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<RiskEntity> items, int total) = await repository.PageRisksAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new RiskPage(await views.DetailsAsync(items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<RiskDetail>>> GetRiskAsync(Guid callerId, Guid riskId, CancellationToken cancellationToken)
    {
        LoadedRisk loaded = await gate.LoadRiskAsync(callerId, PermissionCatalogue.RiskView, riskId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused
            ? refused
            : new Versioned<RiskDetail>(await views.DetailAsync(loaded.Risk!, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(loaded.Risk!));
    }

    public async Task<AdministrationResult<Versioned<RiskDetail>>> RegisterRiskAsync(Guid callerId, RiskDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        ReachedProject reached = await gate.ReachProjectAsync(callerId, PermissionCatalogue.RiskManage, draft.ProjectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        DateTimeOffset now = timeProvider.GetUtcNow();
        AdministrationError? broken = RiskReferences.RegistrationRefused(project)
                                      ?? await references.ProfileRefusedAsync(project, now, cancellationToken).ConfigureAwait(false)
                                      ?? RiskReferences.IdentifiedDateRefused(draft.IdentifiedDate, now)
                                      ?? await references.CategoryRefusedAsync(draft.RiskCategoryItemId, cancellationToken).ConfigureAwait(false)
                                      ?? (draft.OwnerUserId is { } owner ? await references.OwnerRefusedAsync(project, owner, cancellationToken).ConfigureAwait(false) : null);
        if (broken is not null)
        {
            return broken;
        }

        RiskEntity risk = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            Title = draft.Title,
            Description = draft.Description,
            RiskCategoryItemId = draft.RiskCategoryItemId,
            OwnerUserId = draft.OwnerUserId,
            Status = RiskStatus.Identified,
            IdentifiedDate = draft.IdentifiedDate,
            NextReviewDate = draft.NextReviewDate,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        await using IRiskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(risk);
        audit.Stage(RiskAudit.Registered(callerId, project, risk));
        return await gate.SaveRiskAsync(work, risk, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<RiskDetail>>> UpdateRiskAsync(
        Guid callerId, Guid riskId, RiskChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        LoadedRisk loaded = await gate.LoadRiskAsync(callerId, PermissionCatalogue.RiskManage, riskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        RiskEntity risk = loaded.Risk!;
        DateTimeOffset now = timeProvider.GetUtcNow();

        // A reference the risk already holds stays valid: an owner who has since lost their role does not block an edit of the title.
        AdministrationError? broken = RiskReferences.ChangeRefused(project)
                                      ?? (RiskWorkflow.IsOpen(risk.Status) ? null : AdministrationError.Conflict(RiskErrorCodes.Closed))
                                      ?? RiskReferences.IdentifiedDateRefused(changes.IdentifiedDate, now)
                                      ?? (changes.RiskCategoryItemId == risk.RiskCategoryItemId ? null
                                          : await references.CategoryRefusedAsync(changes.RiskCategoryItemId, cancellationToken).ConfigureAwait(false))
                                      ?? (changes.OwnerUserId is { } owner && owner != risk.OwnerUserId
                                          ? await references.OwnerRefusedAsync(project, owner, cancellationToken).ConfigureAwait(false)
                                          : null);
        if (broken is not null)
        {
            return broken;
        }

        RiskFields before = RiskFields.Of(risk);
        risk.Title = changes.Title;
        risk.Description = changes.Description;
        risk.RiskCategoryItemId = changes.RiskCategoryItemId;
        risk.OwnerUserId = changes.OwnerUserId;
        risk.IdentifiedDate = changes.IdentifiedDate;
        risk.NextReviewDate = changes.NextReviewDate;
        RiskGate.Touch(risk, callerId, now);
        await using IRiskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(RiskAudit.Changed(callerId, project, before, risk));
        return await gate.SaveRiskAsync(work, risk, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RiskAssessmentPage> ListAssessmentsAsync(Guid callerId, Guid riskId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableRiskAsync(callerId, riskId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new RiskAssessmentPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<RiskAssessmentVersion> items, int total) = await repository.PageAssessmentsAsync(riskId, page, cancellationToken).ConfigureAwait(false);
        return new RiskAssessmentPage(await views.AssessmentDetailsAsync(items, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<RiskAssessmentDetail>> GetAssessmentAsync(Guid callerId, Guid assessmentId, CancellationToken cancellationToken)
    {
        RiskAssessmentVersion? assessment = await repository.FindAssessmentAsync(assessmentId, cancellationToken).ConfigureAwait(false);
        if (assessment is null)
        {
            return AdministrationError.NotFound;
        }

        LoadedRisk loaded = await gate.LoadRiskAsync(callerId, PermissionCatalogue.RiskView, assessment.RiskId, null, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : (await views.AssessmentDetailsAsync([assessment], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<RiskAcceptancePage> ListAcceptancesAsync(Guid callerId, Guid riskId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableRiskAsync(callerId, riskId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new RiskAcceptancePage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<RiskAcceptance> items, int total) = await repository.PageAcceptancesAsync(riskId, page, cancellationToken).ConfigureAwait(false);
        return new RiskAcceptancePage([.. items.Select(RiskViews.ToDetail)], page.Page, page.PageSize, total);
    }
}
