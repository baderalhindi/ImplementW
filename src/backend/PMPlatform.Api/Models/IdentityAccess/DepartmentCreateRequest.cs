using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-012: a department, optionally under a parent, optionally standing for a directory object.</summary>
public sealed record DepartmentCreateRequest(string? Code, BilingualLabelRequest? Name, Guid? ParentDepartmentId, string? DirectoryReference)
{
    internal List<FieldError> Validate(out DepartmentDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.Code(Code, "code", errors);
        RequestValidation.Label(Name, "name", errors);
        RequestValidation.Optional(DirectoryReference, "directoryReference", 200, errors);
        draft = errors.Count > 0 ? null : new DepartmentDraft(Code!, Name!.ToLabel(), ParentDepartmentId, DirectoryReference);
        return errors;
    }
}
