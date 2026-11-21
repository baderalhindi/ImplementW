using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Models.Project;

/// <summary>
/// How a project's overall progress is calculated (review round 8, point 02): <c>SCHEDULE</c> or <c>STAGES</c>, required. A reason is required
/// by the project once a progress update is approved, and kept in its history either way.
/// </summary>
public sealed record ProgressMethodRequest(string? ProgressMethod, NarrativeTextRequest? Reason = null)
{
    internal List<FieldError> Validate(out ProjectProgressMethod? method, out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        if (string.IsNullOrEmpty(ProgressMethod))
        {
            errors.Add(new FieldError("progressMethod", FieldError.Required));
        }

        method = QueryParameters.OneEnum<ProjectProgressMethod>(ProgressMethod, "progressMethod", errors);
        reason = Reason?.Validate("reason", errors);
        return errors;
    }
}
