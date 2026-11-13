using System.Globalization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// Stage-weighted progress of a period (review round 7, item 1). A period is stage-based when PROJECT_STAGE has a version in force on the day it is
/// started: it copies that version's stages and weights, and its later revisions keep them, so a weight AHDA changes afterwards moves no period already
/// recorded, and a project moves to the new version from its next period. Overall progress is Σ w·c ÷ Σ w over the stages that apply; planned progress
/// is the same sum over each stage's planned % on the period's date. The schedule measures Execution only: while the project has an active baseline,
/// Execution's % is the work breakdown's roll-up, read-only; before that it is entered like the others.
/// </summary>
internal sealed class ProgressStages(IProgressRepository repository, IConfigurationResolver resolver, IProjectStagePlans plans)
{
    /// <summary>The stage the schedule measures.</summary>
    public const string ExecutionStageCode = "EXECUTION";

    private const int NoteLength = 200;

    /// <summary>
    /// Adds the stages of a new period: the version in force now, the plan's applicability and planned %, each % pre-filled from the last published
    /// period, else the plan's starting %, else the project's imported current stage. Null when no version is in force (the period is not
    /// stage-based); <see cref="ProgressErrorCodes.StagePlanRequired"/> when one is and the project has no plan.
    /// </summary>
    public async Task<AdministrationError?> StartAsync(
        ProgressSubmission submission, ProjectFacts project, ProgressInputs read, DateOnly asOf, DateTimeOffset now, Guid actorId, CancellationToken cancellationToken)
    {
        if (await resolver.ResolveStagesAsync(now, cancellationToken).ConfigureAwait(false) is not { } stages)
        {
            return null;
        }

        IReadOnlyList<ProjectStagePlanFact> plan = await plans.ReadAsync(project.Id, cancellationToken).ConfigureAwait(false);
        if (project.StagePlanRevisionNo == 0 || plan.Count == 0)
        {
            return AdministrationError.Rule(ProgressErrorCodes.StagePlanRequired);
        }

        Dictionary<string, ProgressStageEntry> previous = await PreviousAsync(project.Id, cancellationToken).ConfigureAwait(false);
        submission.StageConfigurationVersionId = stages.VersionId;
        int currentIndex = project.CurrentStageCode is { } current ? stages.Stages.ToList().FindIndex(s => s.Code == current) : -1;
        List<ProgressStageEntry> added = [];
        for (int i = 0; i < stages.Stages.Count; i++)
        {
            ProjectStageEntry stage = stages.Stages[i];
            ProjectStagePlanFact? planned = plan.FirstOrDefault(p => p.StageCode == stage.Code);
            ProgressStageEntry? before = previous.GetValueOrDefault(stage.Code);
            decimal? imported = currentIndex < 0 ? null : i < currentIndex ? 100m : i == currentIndex ? project.CurrentStagePercent : 0m;
            ProgressStageEntry entry = new()
            {
                Id = Guid.CreateVersion7(now),
                ProgressSubmissionId = submission.Id,
                StageCode = stage.Code,
                SortOrder = stage.SortOrder,
                WeightPercent = stage.WeightPercent,
                Applies = planned?.Applies ?? true,
                PlannedPercent = PlannedOf(planned, asOf),
                PercentComplete = before?.PercentComplete ?? planned?.StartingPercent ?? imported,
                ActualStartDate = before?.ActualStartDate,
                ActualFinishDate = before?.ActualFinishDate,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            };
            repository.Add(entry);
            added.Add(entry);
        }

        Measure(added, read);
        Calculate(submission, added);
        return null;
    }

    /// <summary>Copies a returned revision's stages to its next revision, as they were entered.</summary>
    public async Task CopyAsync(ProgressSubmission from, ProgressSubmission to, DateTimeOffset now, Guid actorId, CancellationToken cancellationToken)
    {
        foreach (ProgressStageEntry e in await repository.ListStagesAsync(from.Id, track: false, cancellationToken).ConfigureAwait(false))
        {
            repository.Add(new ProgressStageEntry
            {
                Id = Guid.CreateVersion7(now),
                ProgressSubmissionId = to.Id,
                StageCode = e.StageCode,
                SortOrder = e.SortOrder,
                WeightPercent = e.WeightPercent,
                Applies = e.Applies,
                PlannedPercent = e.PlannedPercent,
                PercentComplete = e.PercentComplete,
                IsFromSchedule = e.IsFromSchedule,
                ActualStartDate = e.ActualStartDate,
                ActualFinishDate = e.ActualFinishDate,
                Note = e.Note,
                DecreaseReason = e.DecreaseReason,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }
    }

    /// <summary>Writes the entered stages onto a draft and recalculates it. Field issues when an entry breaks a rule; nothing is written then.</summary>
    public async Task<AdministrationError?> ApplyAsync(
        ProgressSubmission submission, IReadOnlyList<ProgressStageChange> changes, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProgressStageEntry> entries = await repository.ListStagesAsync(submission.Id, track: true, cancellationToken).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return AdministrationError.Rule(ProgressErrorCodes.StageEntryInvalid, new FieldIssue("stages", FieldIssue.NotAllowed));
        }

        Dictionary<string, ProgressStageEntry> previous = await PreviousAsync(submission.ProjectId, cancellationToken).ConfigureAwait(false);
        List<FieldIssue> issues = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < changes.Count; i++)
        {
            ProgressStageChange change = changes[i];
            string path = $"stages[{i.ToString(CultureInfo.InvariantCulture)}]";
            ProgressStageEntry? entry = entries.FirstOrDefault(e => e.StageCode == change.StageCode);
            if (entry is null || !seen.Add(change.StageCode))
            {
                issues.Add(new FieldIssue($"{path}.stageCode", entry is null ? FieldIssue.NotFound : FieldIssue.Duplicate));
                continue;
            }

            if (!entry.Applies)
            {
                if (change.PercentComplete is not null || change.ActualStartDate is not null || change.ActualFinishDate is not null)
                {
                    issues.Add(new FieldIssue($"{path}.percentComplete", FieldIssue.NotAllowed));
                }

                continue;
            }

            if (entry.IsFromSchedule && change.PercentComplete is { } typed && typed != entry.PercentComplete)
            {
                issues.Add(new FieldIssue($"{path}.percentComplete", FieldIssue.NotAllowed));
            }

            if (change.Note is { } note && (note.Length > NoteLength || note.Contains('\n', StringComparison.Ordinal) || note.Contains('\r', StringComparison.Ordinal)))
            {
                issues.Add(new FieldIssue($"{path}.note", FieldIssue.NotAllowed));
            }

            decimal? percent = entry.IsFromSchedule ? entry.PercentComplete : change.PercentComplete;
            bool decreased = percent is { } p && previous.GetValueOrDefault(change.StageCode)?.PercentComplete is { } last && p < last;
            if (!decreased && change.DecreaseReason is not null)
            {
                issues.Add(new FieldIssue($"{path}.decreaseReason", FieldIssue.NotAllowed));
            }

            if (change.ActualStartDate is { } start && change.ActualFinishDate is { } finish && finish < start)
            {
                issues.Add(new FieldIssue($"{path}.actualFinishDate", FieldIssue.BeforeStart));
            }

            if (change.ActualFinishDate is not null && percent is not 100m)
            {
                issues.Add(new FieldIssue($"{path}.actualFinishDate", FieldIssue.NotAllowed));
            }
        }

        if (issues.Count > 0)
        {
            return AdministrationError.Rule(ProgressErrorCodes.StageEntryInvalid, [.. issues]);
        }

        foreach (ProgressStageChange change in changes)
        {
            ProgressStageEntry entry = entries.First(e => e.StageCode == change.StageCode);
            if (!entry.Applies)
            {
                continue;
            }

            if (!entry.IsFromSchedule)
            {
                entry.PercentComplete = change.PercentComplete;
            }

            entry.ActualStartDate = change.ActualStartDate;
            entry.ActualFinishDate = change.ActualFinishDate;
            entry.Note = string.IsNullOrWhiteSpace(change.Note) ? null : change.Note.Trim();
            entry.DecreaseReason = change.DecreaseReason;
            entry.UpdatedAt = now;
            entry.UpdatedBy = actorId;
        }

        Calculate(submission, entries);
        return null;
    }

    /// <summary>
    /// At submission: Execution read from the schedule again, each planned % taken on the period's date, then the rules a complete period meets — every
    /// stage that applies has its %, a started one its actual start, a complete one its finish, a decrease its reason — and the figures fixed.
    /// </summary>
    public async Task<AdministrationError?> SubmitAsync(
        ProgressSubmission submission, ProgressInputs read, DateOnly asOf, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProgressStageEntry> entries = await repository.ListStagesAsync(submission.Id, track: true, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ProjectStagePlanFact> plan = await plans.ReadAsync(submission.ProjectId, cancellationToken).ConfigureAwait(false);
        Dictionary<string, ProgressStageEntry> previous = await PreviousAsync(submission.ProjectId, cancellationToken).ConfigureAwait(false);
        Measure(entries, read);
        List<FieldIssue> issues = [];
        foreach ((ProgressStageEntry entry, int i) in entries.Select((e, i) => (e, i)))
        {
            entry.PlannedPercent = PlannedOf(plan.FirstOrDefault(p => p.StageCode == entry.StageCode), asOf);
            if (!entry.Applies)
            {
                continue;
            }

            string path = $"stages[{i.ToString(CultureInfo.InvariantCulture)}]";
            if (entry.PercentComplete is not { } percent)
            {
                issues.Add(new FieldIssue($"{path}.percentComplete", FieldIssue.Required));
                continue;
            }

            if (percent > 0m && entry.ActualStartDate is null)
            {
                issues.Add(new FieldIssue($"{path}.actualStartDate", FieldIssue.Required));
            }

            if (percent == 100m && entry.ActualFinishDate is null)
            {
                issues.Add(new FieldIssue($"{path}.actualFinishDate", FieldIssue.Required));
            }

            if (previous.GetValueOrDefault(entry.StageCode)?.PercentComplete is { } last && percent < last && entry.DecreaseReason is null)
            {
                issues.Add(new FieldIssue($"{path}.decreaseReason", FieldIssue.Required));
            }
        }

        if (issues.Count > 0)
        {
            return AdministrationError.Rule(ProgressErrorCodes.StageEntryInvalid, [.. issues]);
        }

        Calculate(submission, entries);
        return null;
    }

    /// <summary>The current stage of a period: the first stage that applies below 100%, or the last when all are complete.</summary>
    public static ProgressStageEntry? Current(IEnumerable<ProgressStageEntry> entries) =>
        StageProgress.Current([.. entries.Where(e => e.Applies).OrderBy(e => e.SortOrder)], e => e.PercentComplete ?? 0m);

    /// <summary>The stages of a submission as read, named by the version they were weighed by, beside the last published period's %.</summary>
    public async Task<IReadOnlyList<ProgressStageDetail>> DetailAsync(ProgressSubmission submission, IReadOnlyList<ProgressStageEntry> entries, CancellationToken cancellationToken)
    {
        if (submission.StageConfigurationVersionId is not { } versionId || entries.Count == 0)
        {
            return [];
        }

        ProjectStageSet version = await resolver.ResolvePinnedStagesAsync(versionId, cancellationToken).ConfigureAwait(false);
        Dictionary<string, ProgressStageEntry> previous = submission.Status == ProgressSubmissionStatus.Published
            ? []
            : await PreviousAsync(submission.ProjectId, cancellationToken).ConfigureAwait(false);
        return [.. entries.Select(e => new ProgressStageDetail(
            e.StageCode, version.Find(e.StageCode)?.Name, e.SortOrder, e.WeightPercent, e.Applies, e.PlannedPercent, e.PercentComplete,
            previous.GetValueOrDefault(e.StageCode)?.PercentComplete, e.IsFromSchedule, e.ActualStartDate, e.ActualFinishDate, e.Note, e.DecreaseReason))];
    }

    public async Task<int> VersionNoAsync(Guid versionId, CancellationToken cancellationToken) =>
        (await resolver.ResolvePinnedStagesAsync(versionId, cancellationToken).ConfigureAwait(false)).VersionNo;

    /// <summary>A stage's planned % on <paramref name="asOf"/>, from the plan's dates; null without them or when the stage does not apply.</summary>
    private static decimal? PlannedOf(ProjectStagePlanFact? planned, DateOnly asOf) =>
        planned is { Applies: true, PlannedStartDate: { } start, PlannedEndDate: { } end } ? StageProgress.PlannedPercent(start, end, asOf) : null;

    /// <summary>Execution is the schedule's while an active baseline and a work breakdown give a figure; entered otherwise.</summary>
    private static void Measure(IReadOnlyList<ProgressStageEntry> entries, ProgressInputs read)
    {
        if (entries.FirstOrDefault(e => e.StageCode == ExecutionStageCode) is not { Applies: true } execution)
        {
            return;
        }

        decimal? measured = read.Baseline is null ? null : ProgressRollup.Actual(read.WorkBreakdown);
        if (measured is { } value)
        {
            execution.PercentComplete = decimal.Round(value, 4);
            execution.IsFromSchedule = true;
        }
        else
        {
            execution.IsFromSchedule = false;
        }
    }

    /// <summary>The calculated actual and planned figures of the period from its stages, which are all the period reports.</summary>
    private static void Calculate(ProgressSubmission submission, IReadOnlyList<ProgressStageEntry> entries)
    {
        List<ProgressStageEntry> applicable = [.. entries.Where(e => e.Applies)];
        submission.ActualSource = ProgressActualSource.StageWeighted;
        submission.ActualPercentCalculated = StageProgress.Overall(applicable.Select(e => (e.WeightPercent, (decimal?)(e.PercentComplete ?? 0m)))) ?? 0m;
        submission.PlannedPercent = StageProgress.Overall(applicable.Select(e => (e.WeightPercent, e.PlannedPercent)));
        submission.BaselineId = null;
    }

    private async Task<Dictionary<string, ProgressStageEntry>> PreviousAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.FindLatestPublishedSubmissionAsync(projectId, cancellationToken).ConfigureAwait(false) is { StageConfigurationVersionId: not null } published
            ? (await repository.ListStagesAsync(published.Id, track: false, cancellationToken).ConfigureAwait(false)).ToDictionary(e => e.StageCode, StringComparer.Ordinal)
            : new Dictionary<string, ProgressStageEntry>(StringComparer.Ordinal);
}
