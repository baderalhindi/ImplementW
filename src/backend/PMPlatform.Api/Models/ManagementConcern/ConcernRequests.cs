using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Api.Models.Risk;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Api.Models.ManagementConcern;

/// <summary>
/// A concern's own fields, as a whole (R-5): title, description, category, priority and target resolution date. Its type, status and
/// severity change only through the commands; a severity sent here is an unknown property, and ignored.
/// </summary>
public sealed record ConcernRequest(
    NarrativeTextRequest? Title, NarrativeTextRequest? Description, Guid? CategoryItemId, Guid? PriorityItemId, DateOnly? TargetResolutionDate)
{
    internal List<FieldError> Validate(out ConcernChanges? changes)
    {
        List<FieldError> errors = [];
        (NarrativeText? title, NarrativeText? description) = ValidateInputs(errors);
        changes = errors.Count == 0 ? new ConcernChanges(title!, description!, CategoryItemId!.Value, PriorityItemId!.Value, TargetResolutionDate) : null;
        return errors;
    }

    internal (NarrativeText? Title, NarrativeText? Description) ValidateInputs(List<FieldError> errors)
    {
        NarrativeText? title = RiskRequestValidation.Required(Title, "title", errors);
        NarrativeText? description = RiskRequestValidation.Required(Description, "description", errors);
        RequestValidation.RequireId(CategoryItemId, "categoryItemId", errors);
        RequestValidation.RequireId(PriorityItemId, "priorityItemId", errors);
        return (title, description);
    }
}

/// <summary>
/// A new issue or challenge of the project, OPEN, with the fields <see cref="ConcernRequest"/> describes and, optionally, its impacts on
/// the shared scale. A severity is never sent: the server computes it from the impacts, and ignores one a client adds.
/// </summary>
public sealed record ConcernCreateRequest(
    Guid? ProjectId,
    string? ConcernType,
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Description,
    Guid? CategoryItemId,
    Guid? PriorityItemId,
    IReadOnlyList<ConcernImpactRequest>? Impacts,
    DateOnly? TargetResolutionDate,
    Guid? SeverityItemId = null)
{
    internal List<FieldError> Validate(out ConcernDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        ConcernType? type = ConcernType switch
        {
            "ISSUE" => Domain.ManagementConcern.ConcernType.Issue,
            "CHALLENGE" => Domain.ManagementConcern.ConcernType.Challenge,
            _ => null,
        };
        if (type is null)
        {
            errors.Add(new FieldError("concernType", ConcernType is null ? FieldError.Required : FieldError.OutOfRange));
        }

        (NarrativeText? title, NarrativeText? description) = new ConcernRequest(Title, Description, CategoryItemId, PriorityItemId, TargetResolutionDate).ValidateInputs(errors);
        IReadOnlyList<ConcernImpactInput> impacts = ConcernImpactRequest.Validate(Impacts, required: false, errors);
        draft = errors.Count == 0
            ? new ConcernDraft(ProjectId!.Value, type!.Value, title!, description!, CategoryItemId!.Value, PriorityItemId!.Value, impacts, TargetResolutionDate) { ClaimedSeverityItemId = SeverityItemId }
            : null;
        return errors;
    }
}

/// <summary>An impact level, 1–5, of one dimension of the shared scale (ADR-011); which levels a version defines is the server's rule.</summary>
public sealed record ConcernImpactRequest(Guid? ImpactDimensionItemId, short? ImpactLevel, NarrativeTextRequest? Rationale)
{
    private const int MaxDimensions = 20;

    internal static IReadOnlyList<ConcernImpactInput> Validate(IReadOnlyList<ConcernImpactRequest?>? impacts, bool required, List<FieldError> errors)
    {
        if (impacts is { Count: > MaxDimensions } || (required && impacts is null or []))
        {
            errors.Add(new FieldError("impacts", impacts is null or [] ? FieldError.Required : FieldError.OutOfRange));
            return [];
        }

        List<ConcernImpactInput> inputs = [];
        for (int i = 0; i < (impacts?.Count ?? 0); i++)
        {
            ConcernImpactRequest? impact = impacts![i];
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
                inputs.Add(new ConcernImpactInput(dimension, level, rationale));
            }
        }

        return inputs;
    }
}

/// <summary>A reassessment: the concern's impacts, at least one. Its overall impact and severity are the server's.</summary>
public sealed record ConcernAssessCommand(IReadOnlyList<ConcernImpactRequest?>? Impacts)
{
    internal List<FieldError> Validate(out IReadOnlyList<ConcernImpactInput> impacts)
    {
        List<FieldError> errors = [];
        impacts = ConcernImpactRequest.Validate(Impacts, required: true, errors);
        return errors;
    }
}

public sealed record ConcernAssignCommand(Guid? AssigneeUserId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(AssigneeUserId, "assigneeUserId", errors);
        return errors;
    }
}

/// <summary>The resolution put to validation, or an escalation's direction: required, never blank.</summary>
public sealed record ConcernResolutionCommand(NarrativeTextRequest? Resolution)
{
    internal List<FieldError> Validate(out NarrativeText? resolution)
    {
        List<FieldError> errors = [];
        resolution = RiskRequestValidation.Required(Resolution, "resolution", errors);
        return errors;
    }
}

/// <summary>An escalation of a concern, with why. Where it goes is the server's routing, not the escalator's choice.</summary>
public sealed record ConcernEscalationCreateRequest(Guid? ManagementConcernId, NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(Guid requestKey, out ConcernEscalationDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ManagementConcernId, "managementConcernId", errors);
        NarrativeText? reason = RiskRequestValidation.Required(Reason, "reason", errors);
        draft = errors.Count == 0 ? new ConcernEscalationDraft(ManagementConcernId!.Value, reason!, requestKey) : null;
        return errors;
    }
}
