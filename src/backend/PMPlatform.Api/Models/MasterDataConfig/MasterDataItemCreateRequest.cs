using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>ADM-020–029: a new item of a catalogue, created DRAFT.</summary>
public sealed record MasterDataItemCreateRequest(
    Guid? CatalogueId, string? Code, BilingualLabelRequest? Label, BilingualLabelRequest? Description, Guid? ParentItemId, int? SortOrder)
{
    internal List<FieldError> Validate(out MasterDataItemDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(CatalogueId, "catalogueId", errors);
        RequestValidation.Code(Code, "code", errors);
        RequestValidation.Label(Label, "label", errors);
        BilingualLabel? description = OptionalLabel.Validate(Description, "description", errors);
        draft = errors.Count > 0 ? null : new MasterDataItemDraft(CatalogueId!.Value, Code!, Label!.ToLabel(), description, ParentItemId, SortOrder ?? 0);
        return errors;
    }
}
