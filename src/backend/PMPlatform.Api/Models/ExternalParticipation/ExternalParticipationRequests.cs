using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Api.Models.Risk;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.ExternalParticipation;

/// <summary>
/// A DRAFT request's own fields, as a whole (R-5): the contribution type, the source record its schema applies to (none for a reference-only
/// type), the instructions, the responder and reviewer, and the due date. Its project and entity are fixed when it is drafted.
/// </summary>
public sealed record ExternalUpdateRequestRequest(
    Guid? ContributionTypeItemId, Guid? TargetId, NarrativeTextRequest? Instructions, Guid? ResponsibleUserId, Guid? ReviewerUserId, DateOnly? DueDate)
{
    internal List<FieldError> Validate(out ExternalUpdateRequestChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? instructions = ValidateInputs(errors);
        changes = errors.Count == 0
            ? new ExternalUpdateRequestChanges(ContributionTypeItemId!.Value, TargetId, instructions!, ResponsibleUserId, ReviewerUserId, DueDate)
            : null;
        return errors;
    }

    internal NarrativeText? ValidateInputs(List<FieldError> errors)
    {
        RequestValidation.RequireId(ContributionTypeItemId, "contributionTypeItemId", errors);
        return RiskRequestValidation.Required(Instructions, "instructions", errors);
    }
}

/// <summary>A new request, DRAFT, of one project to one entity, with the fields <see cref="ExternalUpdateRequestRequest"/> describes.</summary>
public sealed record ExternalUpdateRequestCreateRequest(
    Guid? ProjectId,
    Guid? ExternalEntityId,
    Guid? ContributionTypeItemId,
    Guid? TargetId,
    NarrativeTextRequest? Instructions,
    Guid? ResponsibleUserId,
    Guid? ReviewerUserId,
    DateOnly? DueDate)
{
    internal List<FieldError> Validate(out ExternalUpdateRequestDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        RequestValidation.RequireId(ExternalEntityId, "externalEntityId", errors);
        NarrativeText? instructions = new ExternalUpdateRequestRequest(ContributionTypeItemId, TargetId, Instructions, ResponsibleUserId, ReviewerUserId, DueDate).ValidateInputs(errors);
        draft = errors.Count == 0
            ? new ExternalUpdateRequestDraft(ProjectId!.Value, ExternalEntityId!.Value, ContributionTypeItemId!.Value, TargetId, instructions!, ResponsibleUserId, ReviewerUserId, DueDate)
            : null;
        return errors;
    }
}

/// <summary>Why AHDA cancels the request: required.</summary>
public sealed record ExternalUpdateRequestCancelCommand(NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        reason = RiskRequestValidation.Required(Reason, "reason", errors);
        return errors;
    }
}

public sealed record ResponderAssignCommand(Guid? ResponsibleUserId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ResponsibleUserId, "responsibleUserId", errors);
        return errors;
    }
}

public sealed record ReviewerAssignCommand(Guid? ReviewerUserId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ReviewerUserId, "reviewerUserId", errors);
        return errors;
    }
}

/// <summary>
/// One answered field: its code in the request's schema, its value as text — a number in invariant notation, a date as <c>yyyy-MM-dd</c> —
/// and, for a narrative only, its entry language. Whether the code and value fit the schema is the server's rule.
/// </summary>
public sealed record ContributionFieldRequest(string? FieldCode, string? Value, string? Language)
{
    private const int MaxFields = 50;
    private const int CodeLength = 100;

    internal static IReadOnlyList<ContributionFieldInput> Validate(IReadOnlyList<ContributionFieldRequest?>? fields, List<FieldError> errors)
    {
        if (fields is null || fields.Count > MaxFields)
        {
            errors.Add(new FieldError("fields", fields is null ? FieldError.Required : FieldError.OutOfRange));
            return [];
        }

        List<ContributionFieldInput> inputs = [];
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i] is not { } field)
            {
                errors.Add(new FieldError($"fields[{i}]", FieldError.Required));
                continue;
            }

            int before = errors.Count;
            RequestValidation.Require(field.FieldCode, $"fields[{i}].fieldCode", CodeLength, errors);
            RequestValidation.RequireText(field.Value, $"fields[{i}].value", NarrativeTextRequest.TextLength, errors);
            Language? language = field.Language is null ? null : RequestValidation.Language(field.Language, $"fields[{i}].language", fallback: null, errors);
            if (errors.Count == before)
            {
                inputs.Add(new ContributionFieldInput(field.FieldCode!, field.Value!, language));
            }
        }

        return inputs;
    }
}

/// <summary>A DRAFT revision's values, as a whole (R-5).</summary>
public sealed record ExternalContributionRequest(IReadOnlyList<ContributionFieldRequest?>? Fields)
{
    internal List<FieldError> Validate(out IReadOnlyList<ContributionFieldInput> fields)
    {
        List<FieldError> errors = [];
        fields = ContributionFieldRequest.Validate(Fields, errors);
        return errors;
    }
}

/// <summary>The first revision of the answer to an issued request, a DRAFT, with the values given so far.</summary>
public sealed record ExternalContributionCreateRequest(Guid? ExternalUpdateRequestId, IReadOnlyList<ContributionFieldRequest?>? Fields)
{
    internal List<FieldError> Validate(out IReadOnlyList<ContributionFieldInput> fields)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ExternalUpdateRequestId, "externalUpdateRequestId", errors);
        fields = ContributionFieldRequest.Validate(Fields, errors);
        return errors;
    }
}

/// <summary>An acceptance, with AHDA's internal note if any. Nothing in it changes a submitted value.</summary>
public sealed record ContributionAcceptCommand(NarrativeTextRequest? InternalNote)
{
    internal List<FieldError> Validate(out ContributionReviewDecision decision)
    {
        List<FieldError> errors = [];
        decision = new ContributionReviewDecision(null, InternalNote?.Validate("internalNote", errors));
        return errors;
    }
}

/// <summary>A return or a rejection: the reason the entity reads, required, and AHDA's internal note if any. Nothing in it changes a submitted value.</summary>
public sealed record ContributionDecisionCommand(NarrativeTextRequest? Reason, NarrativeTextRequest? InternalNote)
{
    internal List<FieldError> Validate(out ContributionReviewDecision decision)
    {
        List<FieldError> errors = [];
        decision = new ContributionReviewDecision(RiskRequestValidation.Required(Reason, "reason", errors), InternalNote?.Validate("internalNote", errors));
        return errors;
    }
}

/// <summary>An attempt to apply an accepted revision to its source record.</summary>
public sealed record SourceApplicationCreateRequest(Guid? ExternalContributionId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ExternalContributionId, "externalContributionId", errors);
        return errors;
    }
}
