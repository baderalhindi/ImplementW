namespace PMPlatform.Api.Correlation;

/// <summary>
/// api-conventions R-41: <c>X-Correlation-Id</c> is taken from the request when it is a uuid, generated otherwise, echoed
/// on every response and bound to the request's logging scope (R-42).
/// </summary>
internal sealed class CorrelationId(RequestDelegate next, ILogger<CorrelationId> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    private static readonly object ItemKey = new();

    public static Guid Of(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out object? value) && value is Guid id ? id : Guid.Empty;

    public async Task InvokeAsync(HttpContext context)
    {
        Guid id = Guid.TryParse(context.Request.Headers[HeaderName], out Guid supplied) ? supplied : Guid.NewGuid();
        context.Items[ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id.ToString();
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
