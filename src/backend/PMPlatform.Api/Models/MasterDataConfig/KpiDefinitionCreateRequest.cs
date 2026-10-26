using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>A new KPI definition, created DRAFT.</summary>
public sealed record KpiDefinitionCreateRequest(
    string? Code, BilingualLabelRequest? Name, BilingualLabelRequest? Description, Guid? UnitItemId, KpiDirection? Direction)
{
    internal List<FieldError> Validate(out KpiDefinitionDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.Code(Code, "code", errors);
        RequestValidation.Label(Name, "name", errors);
        BilingualLabel? description = OptionalLabel.Validate(Description, "description", errors);
        RequestValidation.RequireId(UnitItemId, "unitItemId", errors);
        if (Direction is null)
        {
            errors.Add(new FieldError("direction", FieldError.Required));
        }

        draft = errors.Count > 0 ? null : new KpiDefinitionDraft(Code!, Name!.ToLabel(), description, UnitItemId!.Value, Direction!.Value);
        return errors;
    }
}
