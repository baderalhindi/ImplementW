using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Api.Models.Project;

/// <summary>Submitting names the project's manager (ADR-013): an R04 holder over the project, internal or of its delivering entity.</summary>
public sealed record ProjectSubmitCommand(Guid? ProjectManagerUserId)
{
    internal List<FieldError> Validate(out ProjectSubmission? submission)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectManagerUserId, "projectManagerUserId", errors);
        submission = errors.Count == 0 ? new ProjectSubmission(ProjectManagerUserId!.Value) : null;
        return errors;
    }
}
