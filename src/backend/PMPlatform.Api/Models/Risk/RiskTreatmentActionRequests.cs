using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Api.Models.Risk;

/// <summary>An action's plan, as a whole (R-5): title, description, type (MITIGATE, AVOID, TRANSFER, CONTINGENCY), owner and due date.</summary>
public sealed record RiskTreatmentActionRequest(
    NarrativeTextRequest? Title, NarrativeTextRequest? Description, string? ActionType, Guid? OwnerUserId, DateOnly? DueDate)
{
    internal List<FieldError> Validate(out RiskTreatmentActionChanges? changes)
    {
        List<FieldError> errors = [];
        (NarrativeText? title, NarrativeText? description, RiskTreatmentActionType? type) = ValidateInputs(errors);
        changes = errors.Count == 0 ? new RiskTreatmentActionChanges(title!, description, type!.Value, OwnerUserId, DueDate) : null;
        return errors;
    }

    internal (NarrativeText? Title, NarrativeText? Description, RiskTreatmentActionType? Type) ValidateInputs(List<FieldError> errors)
    {
        NarrativeText? title = RiskRequestValidation.Required(Title, "title", errors);
        NarrativeText? description = Description?.Validate("description", errors);
        RiskTreatmentActionType? type = ActionType switch
        {
            "MITIGATE" => RiskTreatmentActionType.Mitigate,
            "AVOID" => RiskTreatmentActionType.Avoid,
            "TRANSFER" => RiskTreatmentActionType.Transfer,
            "CONTINGENCY" => RiskTreatmentActionType.Contingency,
            _ => null,
        };
        if (type is null)
        {
            errors.Add(new FieldError("actionType", ActionType is null ? FieldError.Required : FieldError.EnumValue));
        }

        return (title, description, type);
    }
}

/// <summary>A new action of the risk, PLANNED, with the plan <see cref="RiskTreatmentActionRequest"/> describes.</summary>
public sealed record RiskTreatmentActionCreateRequest(
    Guid? RiskId, NarrativeTextRequest? Title, NarrativeTextRequest? Description, string? ActionType, Guid? OwnerUserId, DateOnly? DueDate)
{
    internal List<FieldError> Validate(out RiskTreatmentActionDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(RiskId, "riskId", errors);
        (NarrativeText? title, NarrativeText? description, RiskTreatmentActionType? type) =
            new RiskTreatmentActionRequest(Title, Description, ActionType, OwnerUserId, DueDate).ValidateInputs(errors);
        draft = errors.Count == 0 ? new RiskTreatmentActionDraft(RiskId!.Value, title!, description, type!.Value, OwnerUserId, DueDate) : null;
        return errors;
    }
}
