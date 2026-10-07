using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Api.Models.Risk;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.ChangeRequest;

/// <summary>
/// A change request's own fields, as a whole (R-5): title, justification, the impacts it states and, for a governance-profile change,
/// the profile asked for. A cost impact is an R-16 SAR amount, negative for a reduction; a schedule impact is calendar days, negative to
/// bring the finish forward; neither is zero — no impact is left out. A draft may leave impacts out; what its change type needs is
/// checked on submission. Its type, status and materiality change only through the commands; a materiality sent here is an unknown
/// property, and ignored.
/// </summary>
public sealed record ChangeRequestRequest(
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Justification,
    string? CostImpactSar,
    int? ScheduleImpactDays,
    NarrativeTextRequest? ScopeImpact,
    bool? IsContractualObligation,
    Guid? RequestedGovernanceProfileItemId)
{
    /// <summary>A change moves a finish date by at most this many calendar days either way: ten years.</summary>
    private const int MaxScheduleImpactDays = 3650;

    internal List<FieldError> Validate(out ChangeRequestChanges? changes)
    {
        List<FieldError> errors = [];
        NarrativeText? title = RiskRequestValidation.Required(Title, "title", errors);
        NarrativeText? justification = RiskRequestValidation.Required(Justification, "justification", errors);
        Money? cost = null;
        if (CostImpactSar is not null && (cost = MoneySar.Parse(CostImpactSar)) is null)
        {
            errors.Add(new FieldError("costImpactSar", FieldError.Malformed));
        }
        else if (cost is { Amount: 0 })
        {
            errors.Add(new FieldError("costImpactSar", FieldError.OutOfRange));
        }

        if (ScheduleImpactDays is 0 or < -MaxScheduleImpactDays or > MaxScheduleImpactDays)
        {
            errors.Add(new FieldError("scheduleImpactDays", FieldError.OutOfRange));
        }

        NarrativeText? scope = ScopeImpact?.Validate("scopeImpact", errors);
        changes = errors.Count == 0
            ? new ChangeRequestChanges(title!, justification!, cost, ScheduleImpactDays, scope, IsContractualObligation ?? false, RequestedGovernanceProfileItemId)
            : null;
        return errors;
    }
}

/// <summary>A new change request of the project, DRAFT, of a change type that never changes, with the fields <see cref="ChangeRequestRequest"/> describes.</summary>
public sealed record ChangeRequestCreateRequest(
    Guid? ProjectId,
    ChangeType? ChangeType,
    NarrativeTextRequest? Title,
    NarrativeTextRequest? Justification,
    string? CostImpactSar,
    int? ScheduleImpactDays,
    NarrativeTextRequest? ScopeImpact,
    bool? IsContractualObligation,
    Guid? RequestedGovernanceProfileItemId)
{
    internal List<FieldError> Validate(out ChangeRequestDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        if (ChangeType is null)
        {
            errors.Add(new FieldError("changeType", FieldError.Required));
        }

        errors.AddRange(new ChangeRequestRequest(Title, Justification, CostImpactSar, ScheduleImpactDays, ScopeImpact, IsContractualObligation, RequestedGovernanceProfileItemId)
            .Validate(out ChangeRequestChanges? fields));
        draft = errors.Count == 0 ? new ChangeRequestDraft(ProjectId!.Value, ChangeType!.Value, fields!) : null;
        return errors;
    }
}
