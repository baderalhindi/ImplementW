using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.ProjectTask;

/// <summary>
/// A task's plan and assignment, as a whole (R-5): the schedule activity it executes against, its title and description, its
/// owner and priority, and its planned dates. The planned duration is derived from the dates, never sent; status, actual dates
/// and percentages change only through the commands. An unknown property is ignored.
/// </summary>
public sealed record ProjectTaskRequest(
    Guid? ScheduleActivityId,
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Description,
    Guid? AssigneeUserId,
    Guid? PriorityItemId,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedFinishDate)
{
    internal List<FieldError> Validate(out ProjectTaskChanges? changes)
    {
        List<FieldError> errors = [];
        (NarrativeText? title, NarrativeText? description) = ValidateInputs(errors);
        changes = errors.Count == 0
            ? new ProjectTaskChanges(ScheduleActivityId, title!, description, AssigneeUserId, PriorityItemId, PlannedStartDate!.Value, PlannedFinishDate!.Value)
            : null;
        return errors;
    }

    internal (NarrativeText? Title, NarrativeText? Description) ValidateInputs(List<FieldError> errors)
    {
        if (Title is null)
        {
            errors.Add(new FieldError("title", FieldError.Required));
        }

        NarrativeText? title = Title?.Validate("title", errors);
        NarrativeText? description = Description?.Validate("description", errors);
        if (PlannedStartDate is null)
        {
            errors.Add(new FieldError("plannedStartDate", FieldError.Required));
        }

        if (PlannedFinishDate is null)
        {
            errors.Add(new FieldError("plannedFinishDate", FieldError.Required));
        }
        else if (PlannedStartDate is { } start && PlannedFinishDate < start)
        {
            errors.Add(new FieldError("plannedFinishDate", FieldError.DateBeforeStart));
        }

        return (title, description);
    }
}

/// <summary>A new task of the project, or a subtask of <c>parentTaskId</c>, with the inputs <see cref="ProjectTaskRequest"/> describes.</summary>
public sealed record ProjectTaskCreateRequest(
    Guid? ProjectId,
    Guid? ParentTaskId,
    Guid? ScheduleActivityId,
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Description,
    Guid? AssigneeUserId,
    Guid? PriorityItemId,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedFinishDate)
{
    internal List<FieldError> Validate(out ProjectTaskDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        (NarrativeText? title, NarrativeText? description) =
            new ProjectTaskRequest(ScheduleActivityId, Title, Description, AssigneeUserId, PriorityItemId, PlannedStartDate, PlannedFinishDate).ValidateInputs(errors);
        draft = errors.Count == 0
            ? new ProjectTaskDraft(ProjectId!.Value, ParentTaskId, ScheduleActivityId, title!, description, AssigneeUserId, PriorityItemId, PlannedStartDate!.Value, PlannedFinishDate!.Value)
            : null;
        return errors;
    }
}
