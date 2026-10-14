using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Common.Validation;

namespace PMPlatform.Api.Validation;

/// <summary>
/// TASK-077 (CTL-11): every string an action receives — in its body, its query or its route — obeys <see cref="TextInput"/> before
/// the action runs, so no endpoint can forget to check it. A value that does not is a 400 <c>VALIDATION_FAILED</c> listing each
/// field by its path in the request (<c>title.text</c>, <c>fields[2].value</c>, <c>q</c>) and never the value (R-25). Each request
/// model's own <c>Validate</c> still decides what its fields mean; this filter only holds the rules no field may break.
/// </summary>
/// <remarks>
/// It walks the bound arguments as the platform's request models build them: strings, collections, dictionaries and the public
/// properties of the platform's own types. <c>RequestTextValidationTests</c> fails for an argument of any other type, which this
/// walk would not see into.
/// </remarks>
internal sealed class RequestTextValidation : IActionFilter
{
    /// <summary>Request models are records of records; deeper than this is not a request the platform defines.</summary>
    private const int MaxDepth = 16;

    private static readonly ConcurrentDictionary<Type, (string Name, PropertyInfo Property)[]> Properties = new();

    public void OnActionExecuting(ActionExecutingContext context)
    {
        List<FieldError> errors = [];
        foreach (ParameterDescriptor parameter in context.ActionDescriptor.Parameters)
        {
            if (!context.ActionArguments.TryGetValue(parameter.Name, out object? value))
            {
                continue;
            }

            // R-23: a body's fields are named by their path into the body, a model bound from the query by its properties' names,
            // and anything else by the parameter's own name.
            bool fromRoot = parameter.BindingInfo?.BindingSource == BindingSource.Body || IsModel(parameter.ParameterType);
            Inspect(value, fromRoot ? string.Empty : parameter.Name, errors, depth: 0);
        }

        if (errors.Count > 0)
        {
            context.Result = ApiProblem.Result(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed.", errors);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>The types whose public properties are walked: the platform's own.</summary>
    internal static bool IsModel(Type type) => type.Namespace?.StartsWith("PMPlatform.", StringComparison.Ordinal) == true && !type.IsEnum;

    private static void Inspect(object? value, string path, List<FieldError> errors, int depth)
    {
        if (value is string text)
        {
            if (TextInput.Violation(text) is { } code)
            {
                errors.Add(new FieldError(path, code));
            }
        }
        else if (value is null || depth > MaxDepth || value.GetType().IsPrimitive || value.GetType().IsEnum)
        {
            return;
        }
        else if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                Inspect(key, path, errors, depth + 1);
                Inspect(entry.Value, Member(path, key), errors, depth + 1);
            }
        }
        else if (IsModel(value.GetType()))
        {
            foreach ((string name, PropertyInfo property) in Properties.GetOrAdd(value.GetType(), Readable))
            {
                Inspect(property.GetValue(value), Member(path, name), errors, depth + 1);
            }
        }
        else if (value is IEnumerable items)
        {
            int index = 0;
            foreach (object? item in items)
            {
                Inspect(item, $"{path}[{index++}]", errors, depth + 1);
            }
        }
    }

    private static string Member(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>Each public property with the name it has in JSON (R-15: camelCase unless the model renames it).</summary>
    private static (string, PropertyInfo)[] Readable(Type type) =>
    [
        .. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 && property.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Select(property => (property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name), property)),
    ];
}
