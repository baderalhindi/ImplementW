using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Schedule;

/// <summary>
/// A milestone's inputs, as a whole (R-5): its title, its MILESTONE_CATEGORY item, its Current Forecast date, the activity it
/// completes, if any, and its sort order. The status is set by its commands and by WF-05's acceptance, never sent.
/// </summary>
public sealed record ProjectMilestoneRequest(
    Guid? ScheduleActivityId, NarrativeTextRequest? Title, Guid? MilestoneCategoryItemId, DateOnly? ForecastDate, int? SortOrder)
{
    internal List<FieldError> Validate(out ProjectMilestoneChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? title = ValidateInputs(errors);
        changes = errors.Count == 0 ? new ProjectMilestoneChanges(ScheduleActivityId, title!, MilestoneCategoryItemId!.Value, ForecastDate!.Value, SortOrder ?? 0) : null;
        return errors;
    }

    internal NarrativeText? ValidateInputs(List<FieldError> errors)
    {
        if (Title is null)
        {
            errors.Add(new FieldError("title", FieldError.Required));
        }

        NarrativeText? title = Title?.Validate("title", errors);
        RequestValidation.RequireId(MilestoneCategoryItemId, "milestoneCategoryItemId", errors);
        if (ForecastDate is null)
        {
            errors.Add(new FieldError("forecastDate", FieldError.Required));
        }

        if (SortOrder is < 0)
        {
            errors.Add(new FieldError("sortOrder", FieldError.OutOfRange));
        }

        return title;
    }
}

/// <summary>A new milestone of the project's schedule: the project, and the inputs <see cref="ProjectMilestoneRequest"/> describes.</summary>
public sealed record ProjectMilestoneCreateRequest(
    Guid? ProjectId, Guid? ScheduleActivityId, NarrativeTextRequest? Title, Guid? MilestoneCategoryItemId, DateOnly? ForecastDate, int? SortOrder)
{
    internal List<FieldError> Validate(out ProjectMilestoneDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        NarrativeText? title = new ProjectMilestoneRequest(ScheduleActivityId, Title, MilestoneCategoryItemId, ForecastDate, SortOrder).ValidateInputs(errors);
        draft = errors.Count == 0
            ? new ProjectMilestoneDraft(ProjectId!.Value, ScheduleActivityId, title!, MilestoneCategoryItemId!.Value, ForecastDate!.Value, SortOrder ?? 0)
            : null;
        return errors;
    }
}
