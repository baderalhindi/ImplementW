using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;

namespace PMPlatform.Api.Models.Observability;

/// <summary>Where in the SPA an error was caught.</summary>
public enum ClientErrorSource
{
    /// <summary>A script error no code caught (<c>window</c> <c>error</c> event).</summary>
    WindowError,

    /// <summary>A rejected promise no code handled (<c>unhandledrejection</c>).</summary>
    UnhandledRejection,

    /// <summary>A screen that threw while rendering or loading (the route error boundary).</summary>
    RenderError,
}

/// <summary>
/// An error the SPA could not handle (TASK-090). Its path is the route's path without query or fragment. A failed API call names
/// the correlation id its response carried, so the report lands on the request that caused it.
/// </summary>
public sealed record ClientErrorReportCreateRequest(
    ClientErrorSource? Source,
    string? ErrorType,
    string? Message,
    string? Path,
    string? Stack = null,
    Guid? CorrelationId = null)
{
    public const int ErrorTypeLength = 200;
    public const int MessageLength = 2_000;
    public const int StackLength = 8_000;
    public const int PathLength = 2_000;

    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        if (Source is null)
        {
            errors.Add(new FieldError("source", FieldError.Required));
        }

        RequestValidation.Require(ErrorType, "errorType", ErrorTypeLength, errors);
        RequestValidation.Require(Message, "message", MessageLength, errors);
        RequestValidation.Optional(Stack, "stack", StackLength, errors);
        RequestValidation.Require(Path, "path", PathLength, errors);
        if (Path is { Length: > 0 } && (!Path.StartsWith('/') || Path.IndexOfAny(['?', '#']) >= 0))
        {
            errors.Add(new FieldError("path", FieldError.Malformed));
        }

        if (CorrelationId == Guid.Empty)
        {
            errors.Add(new FieldError("correlationId", FieldError.Malformed));
        }

        return errors;
    }
}
