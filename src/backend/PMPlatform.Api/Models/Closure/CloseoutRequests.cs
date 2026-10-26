using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Api.Models.Risk;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Closure;

/// <summary>
/// A completion case's own fields, as a whole (R-5): the proposed official completion date and the Project Manager's completion narrative.
/// A draft may leave them out; submission requires both. Its status and the project's state change only through the commands.
/// </summary>
public sealed record CompletionCaseRequest(DateOnly? ActualProjectCompletionDate, NarrativeTextRequest? CompletionNarrative)
{
    internal List<FieldError> Validate(out CompletionCaseChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? narrative = CompletionNarrative?.Validate("completionNarrative", errors);
        changes = errors.Count == 0 ? new CompletionCaseChanges(ActualProjectCompletionDate, narrative) : null;
        return errors;
    }
}

/// <summary>A new completion case of an ACTIVE project, DRAFT, with the fields <see cref="CompletionCaseRequest"/> describes.</summary>
public sealed record CompletionCaseCreateRequest(Guid? ProjectId, DateOnly? ActualProjectCompletionDate, NarrativeTextRequest? CompletionNarrative)
{
    internal List<FieldError> Validate(out CompletionCaseDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        errors.AddRange(new CompletionCaseRequest(ActualProjectCompletionDate, CompletionNarrative).Validate(out CompletionCaseChanges? fields));
        draft = errors.Count == 0 ? new CompletionCaseDraft(ProjectId!.Value, fields!) : null;
        return errors;
    }
}

/// <summary>A closure case's own field, as a whole (R-5): the final project summary — on the terminal path, why the project stops.</summary>
public sealed record ClosureCaseRequest(NarrativeTextRequest? ClosureNarrative)
{
    internal List<FieldError> Validate(out ClosureCaseChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? narrative = ClosureNarrative?.Validate("closureNarrative", errors);
        changes = errors.Count == 0 ? new ClosureCaseChanges(narrative) : null;
        return errors;
    }
}

/// <summary>A new closure case, DRAFT: of a COMPLETED project, or of a SUSPENDED one on the terminal path, as the project's state decides.</summary>
public sealed record ClosureCaseCreateRequest(Guid? ProjectId, NarrativeTextRequest? ClosureNarrative)
{
    internal List<FieldError> Validate(out ClosureCaseDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        errors.AddRange(new ClosureCaseRequest(ClosureNarrative).Validate(out ClosureCaseChanges? fields));
        draft = errors.Count == 0 ? new ClosureCaseDraft(ProjectId!.Value, fields!) : null;
        return errors;
    }
}

/// <summary>An accepted exception to a failed, waivable readiness criterion, with why (READY_WITH_CONDITIONS).</summary>
public sealed record CloseoutWaiveCheckCommand(ReadinessCheckCode? CheckCode, NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(out ReadinessWaiver? waiver)
    {
        List<FieldError> errors = [];
        if (CheckCode is null)
        {
            errors.Add(new FieldError("checkCode", FieldError.Required));
        }

        NarrativeText? reason = RiskRequestValidation.Required(Reason, "reason", errors);
        waiver = errors.Count == 0 ? new ReadinessWaiver(CheckCode!.Value, reason!) : null;
        return errors;
    }
}

/// <summary>A post-project obligation's own fields, as a whole (R-5): what is owed, by whom, by when. Its status changes only through the commands.</summary>
public sealed record PostProjectObligationRequest(NarrativeTextRequest? Title, NarrativeTextRequest? Description, Guid? OwnerUserId, DateOnly? DueDate)
{
    internal List<FieldError> Validate(out PostProjectObligationChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? title = RiskRequestValidation.Required(Title, "title", errors);
        NarrativeText? description = Description?.Validate("description", errors);
        changes = errors.Count == 0 ? new PostProjectObligationChanges(title!, description, OwnerUserId, DueDate) : null;
        return errors;
    }
}

/// <summary>A new obligation, OPEN, recorded against a completion case or a terminal closure case — exactly one of the two.</summary>
public sealed record PostProjectObligationCreateRequest(
    Guid? CompletionCaseId, Guid? ClosureCaseId, NarrativeTextRequest? Title, NarrativeTextRequest? Description, Guid? OwnerUserId, DateOnly? DueDate)
{
    internal List<FieldError> Validate(out PostProjectObligationDraft? draft)
    {
        List<FieldError> errors = [];
        if ((CompletionCaseId is null) == (ClosureCaseId is null))
        {
            errors.Add(new FieldError(CompletionCaseId is null ? "completionCaseId" : "closureCaseId", CompletionCaseId is null ? FieldError.Required : FieldError.NotAllowed));
        }

        errors.AddRange(new PostProjectObligationRequest(Title, Description, OwnerUserId, DueDate).Validate(out PostProjectObligationChanges? fields));
        draft = errors.Count == 0 ? new PostProjectObligationDraft(CompletionCaseId, ClosureCaseId, fields!) : null;
        return errors;
    }
}
