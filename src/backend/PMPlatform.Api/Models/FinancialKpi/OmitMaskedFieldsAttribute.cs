using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace PMPlatform.Api.Models.FinancialKpi;

/// <summary>
/// api-conventions R-20(b) on the wire: a field withheld from this caller by ADR-010 classification is omitted from the
/// representation, not sent as null — null means Unknown (TASK-052). The application names each such field in the object's
/// <c>maskedFields</c>; this filter removes those properties from every object of a successful JSON response, the items of a
/// page included, so the SPA renders <i>restricted</i> rather than <i>empty</i>. WF-13 withholds its internal-only fields from an
/// external caller the same way (TASK-066, external-participation.md D-6).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class OmitMaskedFieldsAttribute : Attribute, IAsyncResultFilter
{
    private const string MaskedFields = "maskedFields";

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (context.Result is ObjectResult { Value: { } value, StatusCode: null or < 300 } result)
        {
            JsonSerializerOptions options = context.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
            JsonNode? node = JsonSerializer.SerializeToNode(value, value.GetType(), options);
            Omit(node);
            result.Value = node;
            result.DeclaredType = typeof(JsonNode);
        }

        await next().ConfigureAwait(false);
    }

    private static void Omit(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj[MaskedFields] is JsonArray masked)
                {
                    foreach (string field in masked.Select(f => f!.GetValue<string>()).ToList())
                    {
                        obj.Remove(field);
                    }
                }

                foreach (JsonNode? child in obj.Select(p => p.Value).ToList())
                {
                    Omit(child);
                }

                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    Omit(item);
                }

                break;
            default:
                break;
        }
    }
}
