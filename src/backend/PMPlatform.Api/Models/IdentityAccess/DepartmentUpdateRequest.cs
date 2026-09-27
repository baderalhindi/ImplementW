using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-012: the department's editable representation in full; the code is fixed.</summary>
public sealed record DepartmentUpdateRequest(BilingualLabelRequest? Name, Guid? ParentDepartmentId, string? DirectoryReference)
{
    internal List<FieldError> Validate(out DepartmentChanges? changes)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Name, "name", errors);
        RequestValidation.Optional(DirectoryReference, "directoryReference", 200, errors);
        changes = errors.Count > 0 ? null : new DepartmentChanges(Name!.ToLabel(), ParentDepartmentId, DirectoryReference);
        return errors;
    }
}
