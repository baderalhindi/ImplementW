using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Correlation;

namespace PMPlatform.Api.Errors;

/// <summary>
/// api-conventions R-23: every non-2xx response is RFC 9457 Problem Details with <c>code</c>, <c>correlationId</c>,
/// <c>idempotencyKey</c> and <c>timestamp</c>; a 500 carries nothing more (R-26).
/// </summary>
internal static class ApiProblem
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ObjectResult Result(HttpContext context, int status, string code, string title, IReadOnlyList<FieldError>? errors = null) =>
        new(Create(context, status, code, title, errors)) { StatusCode = status, ContentTypes = { ContentType } };

    /// <summary>For responses written outside MVC: the bearer challenge, the exception handler.</summary>
    public static Task WriteAsync(HttpContext context, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = ContentType;
        return context.Response.WriteAsync(JsonSerializer.Serialize(Create(context, status, code, title, errors: null), Json));
    }

    private static ProblemDetails Create(HttpContext context, int status, string code, string title, IReadOnlyList<FieldError>? errors)
    {
        ProblemDetails problem = new()
        {
            Type = $"urn:pmplatform:problem:{code.ToLowerInvariant().Replace('_', '-')}",
            Title = title,
            Status = status,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationId.Of(context);
        problem.Extensions["idempotencyKey"] = Guid.TryParse(context.Request.Headers["Idempotency-Key"], out Guid key) ? key : null;
        problem.Extensions["timestamp"] = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }
}
