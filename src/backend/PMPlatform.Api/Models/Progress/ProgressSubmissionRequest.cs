using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Progress;

/// <summary>
/// What a person writes on a DRAFT, as a whole (R-5): the narrative and the override. There is no actual or planned
/// percentage: both are derived (ADR-009), and an unknown property is ignored.
/// </summary>
public sealed record ProgressSubmissionRequest(NarrativeTextRequest? Narrative, ProgressOverrideRequest? Override)
{
    internal List<FieldError> Validate(out ProgressSubmissionChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? narrative = Narrative?.Validate("narrative", errors);
        ProgressOverride? progressOverride = Override?.Validate("override", errors);
        changes = errors.Count == 0 ? new ProgressSubmissionChanges(narrative, progressOverride) : null;
        return errors;
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
