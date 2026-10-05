using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Risk;

/// <summary>
/// A risk's register fields, as a whole (R-5): title, description, category, owner, the day it was identified and its next review.
/// Status, rating and closure change only through the commands. An unknown property is ignored.
/// </summary>
public sealed record RiskRequest(
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Description,
    Guid? RiskCategoryItemId,
    Guid? OwnerUserId,
    DateOnly? IdentifiedDate,
    DateOnly? NextReviewDate)
{
    internal List<FieldError> Validate(out RiskChanges? changes)
    {
        List<FieldError> errors = [];
        (NarrativeText? title, NarrativeText? description) = ValidateInputs(errors);
        changes = errors.Count == 0
            ? new RiskChanges(title!, description!, RiskCategoryItemId!.Value, OwnerUserId, IdentifiedDate!.Value, NextReviewDate)
            : null;
        return errors;
    }

    internal (NarrativeText? Title, NarrativeText? Description) ValidateInputs(List<FieldError> errors)
    {
        NarrativeText? title = RiskRequestValidation.Required(Title, "title", errors);
        NarrativeText? description = RiskRequestValidation.Required(Description, "description", errors);
        RequestValidation.RequireId(RiskCategoryItemId, "riskCategoryItemId", errors);
        if (IdentifiedDate is null)
        {
            errors.Add(new FieldError("identifiedDate", FieldError.Required));
        }
        else if (NextReviewDate < IdentifiedDate)
        {
            errors.Add(new FieldError("nextReviewDate", FieldError.DateBeforeStart));
        }

        return (title, description);
    }
}

/// <summary>A new risk of the project, IDENTIFIED, with the fields <see cref="RiskRequest"/> describes. A rating is never sent: it comes from an assessment.</summary>
public sealed record RiskCreateRequest(
    Guid? ProjectId,
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Description,
    Guid? RiskCategoryItemId,
    Guid? OwnerUserId,
    DateOnly? IdentifiedDate,
    DateOnly? NextReviewDate)
{
    internal List<FieldError> Validate(out RiskDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        (NarrativeText? title, NarrativeText? description) =
            new RiskRequest(Title, Description, RiskCategoryItemId, OwnerUserId, IdentifiedDate, NextReviewDate).ValidateInputs(errors);
        draft = errors.Count == 0
            ? new RiskDraft(ProjectId!.Value, title!, description!, RiskCategoryItemId!.Value, OwnerUserId, IdentifiedDate!.Value, NextReviewDate)
            : null;
        return errors;
    }
}

/// <summary>
/// An assessment: a probability level and one impact level for each dimension of the matrix in force, 1–5 (ADR-011). The overall
/// impact and the rating are the server's; a client never sends them.
/// </summary>
public sealed record RiskAssessCommand(short? ProbabilityLevel, IReadOnlyList<RiskImpactRequest>? Impacts, NarrativeTextRequest? Rationale)
{
    private const int MaxDimensions = 20;

    internal List<FieldError> Validate(out RiskAssessmentDraft? draft)
    {
        List<FieldError> errors = [];
        RiskRequestValidation.Level(ProbabilityLevel, "probabilityLevel", errors);
        if (Impacts is not { Count: > 0 and <= MaxDimensions })
        {
            errors.Add(new FieldError("impacts", Impacts is null or [] ? FieldError.Required : FieldError.OutOfRange));
        }

        List<RiskImpactInput> impacts = [];
        for (int i = 0; i < (Impacts?.Count ?? 0) && i < MaxDimensions; i++)
        {
            RiskImpactRequest? impact = Impacts![i];
            if (impact is null)
            {
                errors.Add(new FieldError($"impacts[{i}]", FieldError.Required));
                continue;
            }

            RequestValidation.RequireId(impact.ImpactDimensionItemId, $"impacts[{i}].impactDimensionItemId", errors);
            RiskRequestValidation.Level(impact.ImpactLevel, $"impacts[{i}].impactLevel", errors);
            NarrativeText? rationale = impact.Rationale?.Validate($"impacts[{i}].rationale", errors);
            if (impact.ImpactDimensionItemId is { } dimension && impact.ImpactLevel is { } level)
            {
                impacts.Add(new RiskImpactInput(dimension, level, rationale));
            }
        }

        NarrativeText? overall = Rationale?.Validate("rationale", errors);
        draft = errors.Count == 0 ? new RiskAssessmentDraft(ProbabilityLevel!.Value, impacts, overall) : null;
        return errors;
    }
}

public sealed record RiskImpactRequest(Guid? ImpactDimensionItemId, short? ImpactLevel, NarrativeTextRequest? Rationale);

/// <summary>An acceptance until <c>expiresOn</c>, the first day it no longer holds, with why the risk is tolerated meanwhile.</summary>
public sealed record RiskAcceptCommand(DateOnly? ExpiresOn, NarrativeTextRequest? Rationale)
{
    internal List<FieldError> Validate(out RiskAcceptanceDraft? draft)
    {
        List<FieldError> errors = [];
        if (ExpiresOn is null)
        {
            errors.Add(new FieldError("expiresOn", FieldError.Required));
        }

        NarrativeText? rationale = RiskRequestValidation.Required(Rationale, "rationale", errors);
        draft = errors.Count == 0 ? new RiskAcceptanceDraft(ExpiresOn!.Value, rationale!) : null;
        return errors;
    }
}

/// <summary>Why the risk is closed; required, never empty (TASK-056's closure rationale, enforced here too).</summary>
public sealed record RiskCloseCommand(NarrativeTextRequest? Rationale)
{
    internal List<FieldError> Validate(out NarrativeText? rationale)
    {
        List<FieldError> errors = [];
        rationale = RiskRequestValidation.Required(Rationale, "rationale", errors);
        return errors;
    }
}

/// <summary>The issue to raise from the risk: WF-07's category and priority, and its title and description, the risk's by default.</summary>
public sealed record RiskMaterialiseCommand(Guid? CategoryItemId, Guid? PriorityItemId, NarrativeTextRequest? Title, NarrativeTextRequest? Description)
{
    internal List<FieldError> Validate(out RiskMaterialisationDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(CategoryItemId, "categoryItemId", errors);
        RequestValidation.RequireId(PriorityItemId, "priorityItemId", errors);
        NarrativeText? title = Title?.Validate("title", errors);
        NarrativeText? description = Description?.Validate("description", errors);
        draft = errors.Count == 0 ? new RiskMaterialisationDraft(title, description, CategoryItemId!.Value, PriorityItemId!.Value) : null;
        return errors;
    }
}

internal static class RiskRequestValidation
{
    /// <summary>A required narrative: absent is REQUIRED, and so is blank text.</summary>
    public static NarrativeText? Required(NarrativeTextRequest? value, string field, List<FieldError> errors)
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
            return null;
        }

        return value.Validate(field, errors);
    }

    /// <summary>A level of the five-level scales (ADR-011); which levels a matrix version defines is the server's rule.</summary>
    public static void Level(short? value, string field, List<FieldError> errors)
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
        else if (value is < 1 or > 5)
        {
            errors.Add(new FieldError(field, FieldError.OutOfRange));
        }
    }
}
