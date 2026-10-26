using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Schedule;

/// <summary>
/// An activity's inputs, as a whole (R-5): its place in the work breakdown, its WBS code and name, and — for a leaf — the
/// requested start and the duration in working days. Planned and forecast dates are calculated, never sent (DCL-SCH-03);
/// an unknown property is ignored.
/// </summary>
public sealed record ScheduleActivityRequest(
    Guid? ParentActivityId, string? WbsCode, NarrativeTextRequest? Name, DateOnly? RequestedStartDate, int? PlannedDurationDays, int? SortOrder)
{
    /// <summary>The longest duration accepted, in working days: some forty years, far beyond any project.</summary>
    public const int MaxDurationDays = 9999;

    internal List<FieldError> Validate(out ScheduleActivityChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? name = ValidateInputs(errors);
        changes = errors.Count == 0
            ? new ScheduleActivityChanges(ParentActivityId, WbsCode!, name!, RequestedStartDate!.Value, PlannedDurationDays!.Value, SortOrder ?? 0)
            : null;
        return errors;
    }

    internal NarrativeText? ValidateInputs(List<FieldError> errors)
    {
        RequestValidation.Require(WbsCode, "wbsCode", RequestValidation.CodeLength, errors);
        if (Name is null)
        {
            errors.Add(new FieldError("name", FieldError.Required));
        }

        NarrativeText? name = Name?.Validate("name", errors);
        if (RequestedStartDate is null)
        {
            errors.Add(new FieldError("requestedStartDate", FieldError.Required));
        }

        if (PlannedDurationDays is null)
        {
            errors.Add(new FieldError("plannedDurationDays", FieldError.Required));
        }
        else if (PlannedDurationDays is < 1 or > MaxDurationDays)
        {
            errors.Add(new FieldError("plannedDurationDays", FieldError.OutOfRange));
        }

        if (SortOrder is < 0)
        {
            errors.Add(new FieldError("sortOrder", FieldError.OutOfRange));
        }

        return name;
    }
}

/// <summary>A new activity of the project's schedule: the project, and the inputs <see cref="ScheduleActivityRequest"/> describes.</summary>
public sealed record ScheduleActivityCreateRequest(
    Guid? ProjectId, Guid? ParentActivityId, string? WbsCode, NarrativeTextRequest? Name, DateOnly? RequestedStartDate, int? PlannedDurationDays, int? SortOrder)
{
    internal List<FieldError> Validate(out ScheduleActivityDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        NarrativeText? name = new ScheduleActivityRequest(ParentActivityId, WbsCode, Name, RequestedStartDate, PlannedDurationDays, SortOrder).ValidateInputs(errors);
        draft = errors.Count == 0
            ? new ScheduleActivityDraft(ProjectId!.Value, ParentActivityId, WbsCode!, name!, RequestedStartDate!.Value, PlannedDurationDays!.Value, SortOrder ?? 0)
            : null;
        return errors;
    }
}
