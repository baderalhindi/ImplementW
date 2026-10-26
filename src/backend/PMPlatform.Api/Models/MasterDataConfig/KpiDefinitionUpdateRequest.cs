using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>A KPI definition's editable representation in full; the code is fixed.</summary>
public sealed record KpiDefinitionUpdateRequest(BilingualLabelRequest? Name, BilingualLabelRequest? Description, Guid? UnitItemId, KpiDirection? Direction)
{
    internal List<FieldError> Validate(out KpiDefinitionChanges? changes)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Name, "name", errors);
        BilingualLabel? description = OptionalLabel.Validate(Description, "description", errors);
        RequestValidation.RequireId(UnitItemId, "unitItemId", errors);
        if (Direction is null)
        {
            errors.Add(new FieldError("direction", FieldError.Required));
        }

        changes = errors.Count > 0 ? null : new KpiDefinitionChanges(Name!.ToLabel(), description, UnitItemId!.Value, Direction!.Value);
        return errors;
    }
}
