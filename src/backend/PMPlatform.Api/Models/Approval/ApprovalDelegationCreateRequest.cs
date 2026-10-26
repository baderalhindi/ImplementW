using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.Approval.Contracts;

namespace PMPlatform.Api.Models.Approval;

/// <summary>MOD-043: delegate the caller's approval authority to another internal user for a period, for one routing key or all.</summary>
public sealed record ApprovalDelegationCreateRequest(Guid? DelegateUserId, string? RoutingKey, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo)
{
    public const int RoutingKeyLength = 100;

    internal List<FieldError> Validate(out ApprovalDelegationDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(DelegateUserId, "delegateUserId", errors);
        RequestValidation.Optional(RoutingKey, "routingKey", RoutingKeyLength, errors);
        if (ValidTo is null)
        {
            errors.Add(new FieldError("validTo", FieldError.Required));
        }

        draft = errors.Count > 0 ? null : new ApprovalDelegationDraft(DelegateUserId!.Value, RoutingKey, ValidFrom, ValidTo!.Value);
        return errors;
    }
}
