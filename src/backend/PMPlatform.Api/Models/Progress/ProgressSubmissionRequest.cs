using System.Globalization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Progress;

/// <summary>
/// What a person writes on a DRAFT, as a whole (R-5): the narrative and the override, and on a stage-based period each stage's % complete and dates
/// (review round 7, item 1; omitted, they stay as they are). There is no project-level actual or planned percentage: both are derived (ADR-009), and an
/// unknown property is ignored.
/// </summary>
public sealed record ProgressSubmissionRequest(NarrativeTextRequest? Narrative, ProgressOverrideRequest? Override, IReadOnlyList<ProgressStageRequest>? Stages = null)
{
    private const int MaxStages = 50;

    internal List<FieldError> Validate(out ProgressSubmissionChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? narrative = Narrative?.Validate("narrative", errors);
        ProgressOverride? progressOverride = Override?.Validate("override", errors);
        List<ProgressStageChange>? stages = null;
        if (Stages is { Count: > MaxStages })
        {
            errors.Add(new FieldError("stages", FieldError.OutOfRange));
        }
        else if (Stages is not null)
        {
            stages = [];
            for (int i = 0; i < Stages.Count; i++)
            {
                if (Stages[i]?.Validate($"stages[{i.ToString(CultureInfo.InvariantCulture)}]", errors) is { } stage)
                {
                    stages.Add(stage);
                }
                else if (Stages[i] is null)
                {
                    errors.Add(new FieldError($"stages[{i.ToString(CultureInfo.InvariantCulture)}]", FieldError.Required));
                }
            }
        }

        changes = errors.Count == 0 ? new ProgressSubmissionChanges(narrative, progressOverride, stages) : null;
        return errors;
    }
}

/// <summary>One stage: its code, % complete 0–100 to two places, actual start and finish, an optional one-line note, and why a % went down.</summary>
public sealed record ProgressStageRequest(
    string? StageCode, decimal? PercentComplete = null, DateOnly? ActualStartDate = null, DateOnly? ActualFinishDate = null, string? Note = null,
    NarrativeTextRequest? DecreaseReason = null)
{
    private const int Places = 2;
    private const int NoteLength = 200;

    internal ProgressStageChange? Validate(string field, List<FieldError> errors)
    {
        int before = errors.Count;
        RequestValidation.Code(StageCode, $"{field}.stageCode", errors);
        if (PercentComplete is < 0 or > 100 || (PercentComplete is { } p && decimal.Round(p, Places) != p))
        {
            errors.Add(new FieldError($"{field}.percentComplete", FieldError.OutOfRange));
        }

        RequestValidation.Optional(Note, $"{field}.note", NoteLength, errors);
        NarrativeText? reason = DecreaseReason?.Validate($"{field}.decreaseReason", errors);
        return errors.Count == before
            ? new ProgressStageChange(StageCode!.Trim(), PercentComplete, ActualStartDate, ActualFinishDate, Note, reason)
            : null;
    }
}

/// <summary>ADR-009: an override of the project roll-up, 0–100 to four places, and the reason for it, which is required.</summary>
public sealed record ProgressOverrideRequest(decimal? ActualPercent, NarrativeTextRequest? Reason)
{
    private const int Places = 4;

    internal ProgressOverride? Validate(string field, List<FieldError> errors)
    {
        int before = errors.Count;
        if (ActualPercent is not { } percent)
        {
            errors.Add(new FieldError($"{field}.actualPercent", FieldError.Required));
        }
        else if (percent is < 0 or > 100 || decimal.Round(percent, Places) != percent)
        {
            errors.Add(new FieldError($"{field}.actualPercent", FieldError.OutOfRange));
        }

        if (Reason is null)
        {
            errors.Add(new FieldError($"{field}.reason", FieldError.Required));
        }

        NarrativeText? reason = Reason?.Validate($"{field}.reason", errors);
        return errors.Count == before ? new ProgressOverride(ActualPercent!.Value, reason!) : null;
    }
}
