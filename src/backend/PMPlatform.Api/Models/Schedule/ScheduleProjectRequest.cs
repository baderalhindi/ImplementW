using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;

namespace PMPlatform.Api.Models.Schedule;

/// <summary>A command on a project's schedule that names only the project: initializing it, opening a baseline candidate.</summary>
public sealed record ScheduleProjectRequest(Guid? ProjectId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        return errors;
    }
}
