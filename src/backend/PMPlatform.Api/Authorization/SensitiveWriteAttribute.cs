using Microsoft.AspNetCore.Mvc.Filters;
using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// api-conventions R-35/R-36: a sensitive write requires an <c>Idempotency-Key</c> that is a uuid, and is refused with 400
/// before its body is read otherwise. The replay store of R-37 (<c>common.idempotency_record</c>) is not in the ERD yet
/// (api-conventions S-3, core-platform-schema G-3), so a key is required and echoed but not yet replayed.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class SensitiveWriteAttribute : Attribute, IResourceFilter
{
    public const string HeaderName = "Idempotency-Key";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        string? key = context.HttpContext.Request.Headers[HeaderName];
        if (string.IsNullOrEmpty(key))
        {
            context.Result = ApiProblem.Result(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.IdempotencyKeyRequired, "Idempotency-Key required.");
        }
        else if (!Guid.TryParse(key, out _))
        {
            context.Result = ApiProblem.Result(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.IdempotencyKeyInvalid, "Idempotency-Key must be a uuid.");
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
