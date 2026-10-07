using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Api.Models.Risk;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Api.Models.Suspension;

/// <summary>
/// A suspension or resumption request's own fields, as a whole (R-5): why, from when, and — for a suspension — when the project is
/// expected to resume, as planning information only. A draft may leave the dates out; submission requires the effective date. Its type,
/// status and the project's state change only through the commands; a status sent here is an unknown property, and ignored.
/// </summary>
public sealed record SuspensionRequestRequest(NarrativeTextRequest? Reason, DateOnly? RequestedEffectiveDate, DateOnly? PlannedResumptionDate)
{
    internal List<FieldError> Validate(out SuspensionRequestChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? reason = RiskRequestValidation.Required(Reason, "reason", errors);
        if (PlannedResumptionDate is { } planned && RequestedEffectiveDate is { } effective && planned <= effective)
        {
            errors.Add(new FieldError("plannedResumptionDate", FieldError.DateBeforeStart));
        }

        changes = errors.Count == 0 ? new SuspensionRequestChanges(reason!, RequestedEffectiveDate, PlannedResumptionDate) : null;
        return errors;
    }
}

/// <summary>A new request of the project, DRAFT, of a type that never changes, with the fields <see cref="SuspensionRequestRequest"/> describes.</summary>
public sealed record SuspensionRequestCreateRequest(
    Guid? ProjectId, SuspensionRequestType? RequestType, NarrativeTextRequest? Reason, DateOnly? RequestedEffectiveDate, DateOnly? PlannedResumptionDate)
{
    internal List<FieldError> Validate(out SuspensionRequestDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        if (RequestType is null)
        {
            errors.Add(new FieldError("requestType", FieldError.Required));
        }

        errors.AddRange(new SuspensionRequestRequest(Reason, RequestedEffectiveDate, PlannedResumptionDate).Validate(out SuspensionRequestChanges? fields));
        draft = errors.Count == 0 ? new SuspensionRequestDraft(ProjectId!.Value, RequestType!.Value, fields!) : null;
        return errors;
    }
}
