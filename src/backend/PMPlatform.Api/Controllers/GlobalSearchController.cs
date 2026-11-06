using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.GlobalSearch.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// The header search (global-search.md): open to every signed-in person, because each kind of record is filtered by that person's own view permission
/// and data scope. A person who holds none of them finds nothing.
/// </summary>
[Route("api/v1/search")]
[Tags("GlobalSearch")]
public sealed class GlobalSearchController(IGlobalSearchService search) : AdministrationControllerBase
{
    private const int QueryTextLength = 200;

    /// <summary>The records matching <c>q</c> that the caller may read, at most five of each kind. Under two characters finds nothing.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<GlobalSearchResult>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("GlobalSearch_Search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.Optional(q, "q", QueryTextLength, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await search.SearchAsync(CallerId, q ?? string.Empty, cancellationToken));
    }
}
