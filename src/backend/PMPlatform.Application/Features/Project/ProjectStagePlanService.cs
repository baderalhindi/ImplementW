using System.Globalization;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// A project's stage plan (review round 7, item 1). The stages, their names and weights are the PROJECT_STAGE version in force; the project
/// says which apply and when. AHDA reviews the plan with the registration, so while the registration is DRAFT or RETURNED it changes freely and
/// while it is under review not at all. From approval on, a change to it is a schedule change: it claims the STAGE_PLAN_CHANGE authorisation an
/// approved schedule change request issued for the plan's revision (through <see cref="IStagePlanChangeAuthorization"/>, which ChangeRequest implements). A project that has no plan yet — registered before
/// stages existed, or imported — sets one once without a change request, which is how the 167 imported projects get theirs.
/// </summary>
internal sealed class ProjectStagePlanService(
    IProjectRepository repository,
    ProjectAccess access,
    IConfigurationResolver resolver,
    IStagePlanChangeAuthorization authorizations,
    IAuditTrail audit,
    TimeProvider timeProvider) : IProjectStagePlanService, IProjectStagePlans
{
    public async Task<AdministrationResult<Versioned<ProjectStagePlan>>> GetAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken)
    {
        ProjectEntity? project = await repository.FindAsync(projectId, null, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.ProjectView, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        ProjectStageSet? stages = await resolver.ResolveStagesAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ProjectStagePlanEntry> entries = await repository.ListStagePlanAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new Versioned<ProjectStagePlan>(PlanOf(project, stages, entries), repository.RowVersionOf(project));
    }

    public async Task<AdministrationResult<Versioned<ProjectStagePlan>>> ReplaceAsync(
        Guid callerId, Guid projectId, ProjectStagePlanChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        ProjectEntity? project = await repository.FindAsync(projectId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        if (await CheckWriterAsync(callerId, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (project.LifecycleState == ProjectLifecycleState.Closed)
        {
            return AdministrationError.TerminalState;
        }

        if (project.LifecycleState is ProjectLifecycleState.Submitted or ProjectLifecycleState.UnderReview)
        {
            return AdministrationError.Conflict(ProjectErrorCodes.StagePlanUnderReview);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await resolver.ResolveStagesAsync(now, cancellationToken).ConfigureAwait(false) is not { } stages)
        {
            return AdministrationError.Conflict(ProjectErrorCodes.StagesNotConfigured);
        }

        // A draft may leave a stage's dates for later; submitting it, and any change after approval, needs them (IncompleteAsync).
        if (Invalid(project, stages, changes, requireDates: !ProjectLifecycle.IsEditable(project.LifecycleState)) is { } invalid)
        {
            return invalid;
        }

        IReadOnlyList<ProjectStagePlanEntry> entries = await repository.ListStagePlanAsync(projectId, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string?> before = entries.ToDictionary(e => e.StageCode, e => (string?)Describe(e), StringComparer.Ordinal);
        Dictionary<string, string?> after = changes.Stages.ToDictionary(c => c.StageCode, c => Describe(Normalized(c)), StringComparer.Ordinal);
        bool changed = stages.Stages.Any(s => before.GetValueOrDefault(s.Code) != after.GetValueOrDefault(s.Code));
        if (!changed && project.StagePlanRevisionNo > 0)
        {
            return new Versioned<ProjectStagePlan>(PlanOf(project, stages, entries), repository.RowVersionOf(project));
        }

        bool authorizationRequired = RequiresChangeAuthorization(project);
        if (authorizationRequired)
        {
            if (changes.ChangeAuthorizationId is not { } authorizationId)
            {
                return AdministrationError.Rule(ProjectErrorCodes.StagePlanChangeRequired, new FieldIssue("changeAuthorizationId", FieldIssue.Required));
            }

            if (!await authorizations.ApplyAsync(authorizationId, project.Id, project.StagePlanRevisionNo, callerId, now, cancellationToken).ConfigureAwait(false))
            {
                return AdministrationError.Rule(ProjectErrorCodes.StagePlanChangeRequired, new FieldIssue("changeAuthorizationId", FieldIssue.NotAllowed));
            }
        }

        foreach (ProjectStagePlanEntryChange change in changes.Stages.Select(Normalized))
        {
            ProjectStagePlanEntry? entry = entries.FirstOrDefault(e => e.StageCode == change.StageCode);
            if (entry is null)
            {
                entry = new ProjectStagePlanEntry
                {
                    Id = Guid.CreateVersion7(now),
                    ProjectId = project.Id,
                    StageCode = change.StageCode,
                    CreatedAt = now,
                    CreatedBy = callerId,
                };
                repository.Add(entry);
            }

            entry.Applies = change.Applies;
            entry.PlannedStartDate = change.PlannedStartDate;
            entry.PlannedEndDate = change.PlannedEndDate;
            entry.StartingPercent = change.StartingPercent;
            entry.UpdatedAt = now;
            entry.UpdatedBy = callerId;
        }

        int fromRevisionNo = project.StagePlanRevisionNo;
        project.StagePlanRevisionNo++;
        project.UpdatedAt = now;
        project.UpdatedBy = callerId;
        audit.Stage(ProjectAudit.StagePlanChanged(
            callerId, project, fromRevisionNo, before, after, authorizationRequired ? changes.ChangeAuthorizationId : null, changes.Reason));
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != ProjectSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        IReadOnlyList<ProjectStagePlanEntry> saved = await repository.ListStagePlanAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new Versioned<ProjectStagePlan>(PlanOf(project, stages, saved), repository.RowVersionOf(project));
    }

    public async Task<ProjectStagePage> ListStagesAsync(PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        IReadOnlyList<ProjectStageEntry> stages = (await resolver.ResolveStagesAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))?.Stages ?? [];
        return new ProjectStagePage(
            [.. stages.Skip(page.Skip).Take(page.PageSize).Select(s => new ProjectStagePlanStage(s.Code, s.Name, s.SortOrder, s.WeightPercent, true, null, null, null))],
            page.Page,
            page.PageSize,
            stages.Count);
    }

    public async Task<IReadOnlyList<ProjectStagePlanFact>> ReadAsync(Guid projectId, CancellationToken cancellationToken) =>
        [.. (await repository.ReadStagePlansAsync([projectId], cancellationToken).ConfigureAwait(false))[projectId]
            .Select(e => new ProjectStagePlanFact(e.StageCode, e.Applies, e.PlannedStartDate, e.PlannedEndDate, e.StartingPercent))];

    /// <summary>
    /// What submitting the registration needs of the plan: when stages are configured, every one of them planned, at least one applying, each that
    /// applies dated within the project's dates. Null when it is complete or there are no stages to plan.
    /// </summary>
    internal async Task<AdministrationError?> IncompleteAsync(ProjectEntity project, CancellationToken cancellationToken)
    {
        if (await resolver.ResolveStagesAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false) is not { } stages)
        {
            return null;
        }

        IReadOnlyList<ProjectStagePlanEntry> entries = await repository.ListStagePlanAsync(project.Id, cancellationToken).ConfigureAwait(false);
        ProjectStagePlanChanges asWritten = new(
            [.. stages.Stages.Select(s => entries.FirstOrDefault(e => e.StageCode == s.Code) is { } e
                ? new ProjectStagePlanEntryChange(e.StageCode, e.Applies, e.PlannedStartDate, e.PlannedEndDate, e.StartingPercent)
                : new ProjectStagePlanEntryChange(s.Code, true, null, null, null))],
            null, null);
        return Invalid(project, stages, asWritten, requireDates: true) is null ? null : AdministrationError.Rule(ProjectErrorCodes.Incomplete, new FieldIssue("stagePlan", FieldIssue.Required));
    }

    /// <summary>The registration's writer while it is DRAFT or RETURNED; from approval on, whoever records the project's progress may set it too.</summary>
    private async Task<AdministrationError?> CheckWriterAsync(Guid callerId, ProjectEntity project, CancellationToken cancellationToken) =>
        !ProjectLifecycle.IsEditable(project.LifecycleState)
        && await access.HoldsAsync(callerId, PermissionCatalogue.ProgressSubmit, project, cancellationToken).ConfigureAwait(false)
            ? null
            : await access.CheckAsync(callerId, PermissionCatalogue.ProjectRegister, project, cancellationToken).ConfigureAwait(false);

    private static bool RequiresChangeAuthorization(ProjectEntity project) =>
        !ProjectLifecycle.IsEditable(project.LifecycleState) && project.StagePlanRevisionNo > 0;

    /// <summary>A project already under way when it entered the platform: taken in (ADR-014), imported, or activated.</summary>
    private static bool AcceptsStartingPercent(ProjectEntity project) =>
        project.LegacyIntakeDate is not null || project.ProjectImportBatchId is not null || project.ActivatedAt is not null;

    private static AdministrationError? Invalid(ProjectEntity project, ProjectStageSet stages, ProjectStagePlanChanges changes, bool requireDates)
    {
        List<FieldIssue> issues = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < changes.Stages.Count; i++)
        {
            ProjectStagePlanEntryChange stage = changes.Stages[i];
            string path = $"stages[{i.ToString(CultureInfo.InvariantCulture)}]";
            if (stages.Find(stage.StageCode) is null)
            {
                issues.Add(new FieldIssue($"{path}.stageCode", FieldIssue.NotFound));
                continue;
            }

            if (!seen.Add(stage.StageCode))
            {
                issues.Add(new FieldIssue($"{path}.stageCode", FieldIssue.Duplicate));
                continue;
            }

            if (!stage.Applies)
            {
                continue;
            }

            if (stage.PlannedStartDate is not { } start)
            {
                if (requireDates)
                {
                    issues.Add(new FieldIssue($"{path}.plannedStartDate", FieldIssue.Required));
                }
            }
            else if (project.PlannedStartDate is { } projectStart && start < projectStart)
            {
                issues.Add(new FieldIssue($"{path}.plannedStartDate", FieldIssue.BeforeStart));
            }

            if (stage.PlannedEndDate is not { } end)
            {
                if (requireDates)
                {
                    issues.Add(new FieldIssue($"{path}.plannedEndDate", FieldIssue.Required));
                }
            }
            else if ((stage.PlannedStartDate is { } from && end < from) || (project.PlannedEndDate is { } projectEnd && end > projectEnd))
            {
                issues.Add(new FieldIssue($"{path}.plannedEndDate", FieldIssue.OutsideRange));
            }

            if (stage.StartingPercent is { } percent && (!AcceptsStartingPercent(project) || percent is < 0m or > 100m))
            {
                issues.Add(new FieldIssue($"{path}.startingPercent", FieldIssue.NotAllowed));
            }
        }

        if (stages.Stages.Any(s => !seen.Contains(s.Code)))
        {
            issues.Add(new FieldIssue("stages", FieldIssue.Required));
        }
        else if (issues.Count == 0 && !changes.Stages.Any(s => s.Applies))
        {
            // A project to which no stage applies has no progress to weigh.
            issues.Add(new FieldIssue("stages", FieldIssue.NotAllowed));
        }

        return issues.Count == 0 ? null : AdministrationError.Rule(ProjectErrorCodes.StagePlanInvalid, [.. issues]);
    }

    /// <summary>A stage that does not apply keeps no dates and no starting %.</summary>
    private static ProjectStagePlanEntryChange Normalized(ProjectStagePlanEntryChange change) =>
        change.Applies ? change : change with { PlannedStartDate = null, PlannedEndDate = null, StartingPercent = null };

    private static string Describe(ProjectStagePlanEntry entry) =>
        Describe(new ProjectStagePlanEntryChange(entry.StageCode, entry.Applies, entry.PlannedStartDate, entry.PlannedEndDate, entry.StartingPercent))!;

    /// <summary>A stage's plan in one audit value: <c>applies 2026-01-01..2026-03-31 starting 40</c>, or <c>does not apply</c>.</summary>
    private static string? Describe(ProjectStagePlanEntryChange change) =>
        !change.Applies
            ? "does not apply"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"applies {change.PlannedStartDate:yyyy-MM-dd}..{change.PlannedEndDate:yyyy-MM-dd}{(change.StartingPercent is { } p ? $" starting {p:0.##}" : string.Empty)}");

    private static ProjectStagePlan PlanOf(ProjectEntity project, ProjectStageSet? stages, IReadOnlyList<ProjectStagePlanEntry> entries) =>
        new(
            project.Id,
            project.StagePlanRevisionNo,
            RequiresChangeAuthorization(project),
            AcceptsStartingPercent(project),
            stages?.VersionId,
            stages?.VersionNo,
            stages is null
                ? []
                : [.. stages.Stages.Select(s => entries.FirstOrDefault(e => e.StageCode == s.Code) is { } e
                    ? new ProjectStagePlanStage(s.Code, s.Name, s.SortOrder, s.WeightPercent, e.Applies, e.PlannedStartDate, e.PlannedEndDate, e.StartingPercent)
                    : new ProjectStagePlanStage(s.Code, s.Name, s.SortOrder, s.WeightPercent, true, null, null, null))]);
}
