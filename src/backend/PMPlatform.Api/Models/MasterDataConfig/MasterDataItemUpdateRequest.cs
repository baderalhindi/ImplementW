using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>ADM-020–029: an item's editable representation in full; the catalogue and code are fixed.</summary>
public sealed record MasterDataItemUpdateRequest(BilingualLabelRequest? Label, BilingualLabelRequest? Description, Guid? ParentItemId, int? SortOrder)
{
    internal List<FieldError> Validate(out MasterDataItemChanges? changes)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Label, "label", errors);
        BilingualLabel? description = OptionalLabel.Validate(Description, "description", errors);
        if (SortOrder is null)
        {
            errors.Add(new FieldError("sortOrder", FieldError.Required));
        }

        changes = errors.Count > 0 ? null : new MasterDataItemChanges(Label!.ToLabel(), description, ParentItemId, SortOrder!.Value);
        return errors;
    }
}
