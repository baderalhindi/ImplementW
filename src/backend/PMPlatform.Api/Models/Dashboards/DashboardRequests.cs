using System.Text.Json;
using System.Text.RegularExpressions;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Api.Models.Dashboards;

/// <summary>A dashboard named in a route: one of the three of ADR-006 by its R-19 code; any other name is no dashboard (404).</summary>
internal static class DashboardRoute
{
    public static DashboardCode? Parse(string value) =>
        Enum.GetValues<DashboardCode>().Cast<DashboardCode?>().FirstOrDefault(c => JsonNamingPolicy.SnakeCaseUpper.ConvertName(c!.Value.ToString()) == value);
}

/// <summary>A new version of one of the three dashboards; it opens as a DRAFT copy of the PUBLISHED version.</summary>
public sealed record DashboardDefinitionCreateRequest(DashboardCode? Code)
{
    internal List<FieldError> Validate(out DashboardCode code)
    {
        List<FieldError> errors = [];
        if (Code is null)
        {
            errors.Add(new FieldError("code", FieldError.Required));
        }

        code = Code ?? default;
        return errors;
    }
}

/// <summary>
/// A DRAFT version's content, as a whole (R-5): bilingual labels (ADR-012), personalisation, audience and widgets. A widget names a
/// registered projection by its code — nothing else: no query, formula or script is accepted anywhere (BR-DSH-030). Whether the codes
/// are registered, supported and laid out without overlap is the module's validation.
/// </summary>
public sealed record DashboardDefinitionRequest(
    BilingualLabelRequest? Name,
    BilingualLabelRequest? Description,
    bool? AllowsPersonalization,
    IReadOnlyList<DashboardAudienceRequest>? Audience,
    IReadOnlyList<DashboardWidgetRequest>? Widgets)
{
    internal const int MaxWidgets = 48;

    internal List<FieldError> Validate(out DashboardDefinitionContent? content)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Name, "name", errors);
        if (Description is not null)
        {
            RequestValidation.Label(Description, "description", errors);
        }

        if (AllowsPersonalization is null)
        {
            errors.Add(new FieldError("allowsPersonalization", FieldError.Required));
        }

        if (Audience is null)
        {
            errors.Add(new FieldError("audience", FieldError.Required));
        }
        else
        {
            for (int i = 0; i < Audience.Count; i++)
            {
                RequestValidation.Code(Audience[i]?.RoleCode, $"audience[{i}].roleCode", errors);
            }
        }

        if (Widgets is null)
        {
            errors.Add(new FieldError("widgets", FieldError.Required));
        }
        else if (Widgets.Count > MaxWidgets)
        {
            errors.Add(new FieldError("widgets", FieldError.OutOfRange));
        }
        else
        {
            for (int i = 0; i < Widgets.Count; i++)
            {
                Widgets[i]?.Validate($"widgets[{i}]", errors);
                if (Widgets[i] is null)
                {
                    errors.Add(new FieldError($"widgets[{i}]", FieldError.Required));
                }
            }
        }

        content = errors.Count == 0
            ? new DashboardDefinitionContent(
                Name!.ToLabel(),
                Description?.ToLabel(),
                AllowsPersonalization!.Value,
                [.. Audience!.Select(a => new DashboardAudienceInput(a.RoleCode!, a.IsDefaultLanding ?? false))],
                [.. Widgets!.Select(w => w.ToInput())])
            : null;
        return errors;
    }
}

public sealed record DashboardAudienceRequest(string? RoleCode, bool? IsDefaultLanding);

public sealed partial record DashboardWidgetRequest(
    string? Code,
    BilingualLabelRequest? Title,
    DashboardWidgetType? WidgetType,
    string? SourceProjectionCode,
    Guid? DataClassificationItemId,
    bool? IsOptionalVisibility,
    short? LayoutRow,
    short? LayoutColumn,
    short? LayoutSpan)
{
    internal void Validate(string at, List<FieldError> errors)
    {
        Shape(Code, $"{at}.code", RequestValidation.CodeLength, WidgetCodeShape(), errors);
        RequestValidation.Label(Title, $"{at}.title", errors);
        if (WidgetType is null)
        {
            errors.Add(new FieldError($"{at}.widgetType", FieldError.Required));
        }

        Shape(SourceProjectionCode, $"{at}.sourceProjectionCode", 100, ProjectionCodeShape(), errors);
        if (DataClassificationItemId == Guid.Empty)
        {
            errors.Add(new FieldError($"{at}.dataClassificationItemId", FieldError.Malformed));
        }

        // A twelve-column grid, one row high per widget; the database checks the same bounds.
        if (LayoutRow is not >= 1)
        {
            errors.Add(new FieldError($"{at}.layoutRow", LayoutRow is null ? FieldError.Required : FieldError.OutOfRange));
        }

        if (LayoutColumn is not (>= 1 and <= 12))
        {
            errors.Add(new FieldError($"{at}.layoutColumn", LayoutColumn is null ? FieldError.Required : FieldError.OutOfRange));
        }

        if (LayoutSpan is not (>= 1 and <= 12) || (LayoutColumn is { } column && column + LayoutSpan > 13))
        {
            errors.Add(new FieldError($"{at}.layoutSpan", LayoutSpan is null ? FieldError.Required : FieldError.OutOfRange));
        }
    }

    internal DashboardWidgetInput ToInput() =>
        new(Code!, Title!.ToLabel(), WidgetType!.Value, SourceProjectionCode!, DataClassificationItemId, IsOptionalVisibility ?? false, LayoutRow!.Value, LayoutColumn!.Value, LayoutSpan!.Value);

    private static void Shape(string? value, string field, int maxLength, Regex shape, List<FieldError> errors)
    {
        RequestValidation.Require(value, field, maxLength, errors);
        if (!string.IsNullOrEmpty(value) && value.Length <= maxLength && !shape.IsMatch(value))
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex WidgetCodeShape();

    /// <summary><c>&lt;SOURCE&gt;.&lt;PROJECTION&gt;</c>: a name, never an expression; the database checks the same pattern.</summary>
    [GeneratedRegex(@"^[A-Z][A-Z_]*\.[A-Z][A-Z_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectionCodeShape();
}

/// <summary>ADR-019: the caller's choices for the optional widgets, as a whole; a widget not named keeps the governed layout.</summary>
public sealed record DashboardPersonalizationRequest(IReadOnlyList<WidgetPersonalizationRequest>? Widgets)
{
    internal List<FieldError> Validate(out DashboardPersonalizationInput? input)
    {
        List<FieldError> errors = [];
        if (Widgets is null)
        {
            errors.Add(new FieldError("widgets", FieldError.Required));
        }
        else
        {
            for (int i = 0; i < Widgets.Count; i++)
            {
                RequestValidation.Require(Widgets[i]?.WidgetCode, $"widgets[{i}].widgetCode", RequestValidation.CodeLength, errors);
                if (Widgets[i]?.SortOrder is < 1)
                {
                    errors.Add(new FieldError($"widgets[{i}].sortOrder", FieldError.OutOfRange));
                }
            }
        }

        input = errors.Count == 0
            ? new DashboardPersonalizationInput([.. Widgets!.Select(w => new WidgetPersonalizationInput(w.WidgetCode!, w.IsHidden ?? false, w.SortOrder))])
            : null;
        return errors;
    }
}

public sealed record WidgetPersonalizationRequest(string? WidgetCode, bool? IsHidden, short? SortOrder);
