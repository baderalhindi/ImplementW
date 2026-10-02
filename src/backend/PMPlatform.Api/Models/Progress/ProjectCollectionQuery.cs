using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.Progress;

/// <summary>
/// The query of every WF-02 collection: one project's records, one page (R-3, R-29). <c>projectId</c> is required,
/// because the progress tables hold no anchor of their own to filter a caller's scope by.
/// </summary>
internal static class ProjectCollectionQuery
{
    public static List<FieldError> Validate(Guid? projectId, int? page, int? pageSize, out PageRequest paging)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        paging = QueryParameters.Page(page, pageSize, errors);
        return errors;
    }
}
