using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;

namespace PMPlatform.Api.Models.Progress;

/// <summary>Starts the progress update of the project's next period. The figures are derived; nothing else is sent.</summary>
public sealed record ProgressSubmissionStartRequest(Guid? ProjectId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        return errors;
    }
}
