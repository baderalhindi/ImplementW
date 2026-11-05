using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models;

/// <summary>One project of a batch; a responder given here is for a project with no entity contact, and becomes it.</summary>
public sealed record UpdateRequestBatchItemRequest(Guid? ProjectId, Guid? ResponsibleUserId = null);

/// <summary>The monthly request: the projects, the due date, whether it repeats and on which day, instructions and the reviewer.</summary>
public sealed record UpdateRequestBatchRequest(
    IReadOnlyList<UpdateRequestBatchItemRequest>? Items, DateOnly? DueDate, bool? RepeatMonthly, int? RepeatDay, string? Instructions, Guid? ReviewerUserId, string? InstructionsLanguage = null)
{
    private const int MaxInstructions = 4000;

    internal List<FieldError> Validate(out UpdateRequestBatchDraft? draft)
    {
        List<FieldError> errors = [];
        if (Items is null || Items.Count == 0 || Items.Any(i => i.ProjectId is null))
        {
            errors.Add(new FieldError("items", FieldError.Required));
        }

        if (DueDate is null)
        {
            errors.Add(new FieldError("dueDate", FieldError.Required));
        }

        if (RepeatMonthly == true && RepeatDay is not (>= 1 and <= 28))
        {
            errors.Add(new FieldError("repeatDay", FieldError.OutOfRange));
        }

        RequestValidation.RequireText(Instructions, "instructions", MaxInstructions, errors);
        Language language = RequestValidation.Language(InstructionsLanguage, "instructionsLanguage", Language.Ar, errors);
        RequestValidation.RequireId(ReviewerUserId, "reviewerUserId", errors);
        draft = errors.Count > 0
            ? null
            : new UpdateRequestBatchDraft(
                [.. Items!.Select(i => new UpdateRequestBatchItem(i.ProjectId!.Value, i.ResponsibleUserId))], DueDate!.Value, RepeatMonthly == true,
                RepeatMonthly == true ? RepeatDay : null, new NarrativeText(Instructions!, language), ReviewerUserId!.Value);
        return errors;
    }
}

public sealed record UpdateRequestBatchWithdrawRequest(string? Reason, string? ReasonLanguage = null)
{
    internal List<FieldError> Validate(out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireText(Reason, "reason", 2000, errors);
        Language language = RequestValidation.Language(ReasonLanguage, "reasonLanguage", Language.Ar, errors);
        reason = errors.Count > 0 ? null : new NarrativeText(Reason!, language);
        return errors;
    }
}
