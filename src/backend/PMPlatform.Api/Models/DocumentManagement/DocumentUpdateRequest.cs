using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.DocumentManagement.Contracts;

namespace PMPlatform.Api.Models.DocumentManagement;

/// <summary>MOD-051 Edit Metadata: the whole editable set (R-5).</summary>
public sealed record DocumentUpdateRequest(
    NarrativeTextRequest? Title, NarrativeTextRequest? Description, Guid? DocumentTypeItemId, Guid? DataClassificationItemId)
{
    internal List<FieldError> Validate(out DocumentChanges? changes)
    {
        List<FieldError> errors = [];
        if (Title is null)
        {
            errors.Add(new FieldError("title", FieldError.Required));
        }

        Domain.Common.NarrativeText? title = Title?.Validate("title", errors);
        Domain.Common.NarrativeText? description = Description?.Validate("description", errors);
        RequestValidation.RequireId(DocumentTypeItemId, "documentTypeItemId", errors);
        RequestValidation.RequireId(DataClassificationItemId, "dataClassificationItemId", errors);
        changes = errors.Count == 0 ? new DocumentChanges(title!, description, DocumentTypeItemId!.Value, DataClassificationItemId!.Value) : null;
        return errors;
    }
}
