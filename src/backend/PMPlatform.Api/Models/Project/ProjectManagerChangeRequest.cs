using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Project;

/// <summary>The new Project Manager and why the project changes hands (review round 7, item 2). Both are required.</summary>
public sealed record ProjectManagerChangeRequest(Guid? ProjectManagerUserId, NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(out ProjectManagerChange? change)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectManagerUserId, "projectManagerUserId", errors);
        if (Reason is null)
        {
            errors.Add(new FieldError("reason", FieldError.Required));
        }

        NarrativeText? reason = Reason?.Validate("reason", errors);
        change = errors.Count == 0 ? new ProjectManagerChange(ProjectManagerUserId!.Value, reason!) : null;
        return errors;
    }
}
