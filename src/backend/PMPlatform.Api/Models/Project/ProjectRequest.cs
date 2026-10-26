using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Project;

/// <summary>
/// MOD-001 Create Project and the edit of a DRAFT or RETURNED project: the whole registration (R-5). There is no
/// <c>formalProjectId</c>: AHDA issues it on approval (ADR-013).
/// </summary>
public sealed record ProjectRequest(
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Description,
    Guid? ClassificationItemId,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    string? ParticipationMode,
    Guid? GovernanceProfileItemId,
    string? RegistrationBudgetSar,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    Guid? RegionItemId,
    Guid? CityItemId,
    decimal? Latitude,
    decimal? Longitude)
{
    internal List<FieldError> Validate(out ProjectDraft? draft)
    {
        List<FieldError> errors = [];
        if (Title is null)
        {
            errors.Add(new FieldError("title", FieldError.Required));
        }

        NarrativeText? title = Title?.Validate("title", errors);
        NarrativeText? description = Description?.Validate("description", errors);
        RequestValidation.RequireId(ClassificationItemId, "classificationItemId", errors);
        RequestValidation.RequireId(DepartmentId, "departmentId", errors);
        RequestValidation.RequireId(GovernanceProfileItemId, "governanceProfileItemId", errors);
        ParticipationMode? mode = ParticipationModeOf(errors);
        Money? budget = Budget(errors);

        if (PlannedStartDate is { } start && PlannedEndDate is { } end && end < start)
        {
            errors.Add(new FieldError("plannedEndDate", FieldError.DateBeforeStart));
        }

        if (Latitude is < -90 or > 90)
        {
            errors.Add(new FieldError("latitude", FieldError.OutOfRange));
        }

        if (Longitude is < -180 or > 180)
        {
            errors.Add(new FieldError("longitude", FieldError.OutOfRange));
        }

        draft = errors.Count == 0
            ? new ProjectDraft(
                title!, description, ClassificationItemId!.Value, DepartmentId!.Value, ExternalEntityId, mode!.Value, GovernanceProfileItemId!.Value, budget,
                PlannedStartDate, PlannedEndDate, RegionItemId, CityItemId, Latitude, Longitude)
            : null;
        return errors;
    }

    private ParticipationMode? ParticipationModeOf(List<FieldError> errors)
    {
        switch (ParticipationMode)
        {
            case "ENTITY_MANAGED":
                return Domain.Common.ParticipationMode.EntityManaged;
            case "AHDA_MANAGED":
                return Domain.Common.ParticipationMode.AhdaManaged;
            case null:
                errors.Add(new FieldError("participationMode", FieldError.Required));
                return null;
            default:
                errors.Add(new FieldError("participationMode", FieldError.EnumValue));
                return null;
        }
    }

    /// <summary>R-16, and not negative: a stated budget is an amount to be spent.</summary>
    private Money? Budget(List<FieldError> errors)
    {
        if (RegistrationBudgetSar is null)
        {
            return null;
        }

        Money? budget = MoneySar.Parse(RegistrationBudgetSar);
        if (budget is null)
        {
            errors.Add(new FieldError("registrationBudgetSar", FieldError.Malformed));
        }
        else if (budget.Value.Amount < 0)
        {
            errors.Add(new FieldError("registrationBudgetSar", FieldError.OutOfRange));
        }

        return budget;
    }
}
