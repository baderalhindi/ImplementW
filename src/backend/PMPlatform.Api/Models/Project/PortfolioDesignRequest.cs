using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Models.Project;

/// <summary>
/// The Portfolio dashboard's design as a whole, in reading order (the 17 Nov 2026 designer). Each field is required here; how they fit together — a grouping
/// the type takes, a place on the grid no other visual holds — is checked by the service (422 <c>PROJECT_PORTFOLIO_DESIGN_INVALID</c>). A container's
/// <c>children</c> (the 19 Nov 2026 containers) are visuals of the same shape, counted in the design's 60.
/// </summary>
public sealed record PortfolioDesignRequest(IReadOnlyList<PortfolioVisualRequest?>? Visuals)
{
    internal List<FieldError> Validate(out PortfolioDesignInput? design)
    {
        List<FieldError> errors = [];
        if (Visuals is null)
        {
            errors.Add(new FieldError("visuals", FieldError.Required));
        }
        else if (Visuals.Sum(v => 1 + (v?.Children?.Count ?? 0)) > PortfolioDesignInput.MaxVisuals)
        {
            errors.Add(new FieldError("visuals", FieldError.OutOfRange));
        }
        else
        {
            for (int i = 0; i < Visuals.Count; i++)
            {
                if (Visuals[i] is { } visual)
                {
                    visual.Validate($"visuals[{i}]", errors);
                }
                else
                {
                    errors.Add(new FieldError($"visuals[{i}]", FieldError.Required));
                }
            }
        }

        design = errors.Count == 0 ? new PortfolioDesignInput([.. Visuals!.Select(v => v!.ToInput())]) : null;
        return errors;
    }
}

public sealed record PortfolioVisualRequest(
    BilingualLabelRequest? Title,
    PortfolioVisualType? VisualType,
    IReadOnlyList<PortfolioMeasure>? Measures,
    PortfolioGroupBy? GroupBy,
    decimal? LayoutColumn,
    decimal? LayoutRow,
    decimal? LayoutWidth,
    decimal? LayoutHeight,
    PortfolioSection? Section = null,
    IReadOnlyList<PortfolioFieldValueRequest?>? Values = null,
    string? GroupField = null,
    IReadOnlyList<PortfolioFieldFilterRequest?>? Filters = null,
    PortfolioDateUnit? GroupDateUnit = null,
    PortfolioAxesRequest? Axes = null,
    string? WidgetCode = null,
    IReadOnlyList<PortfolioVisualRequest?>? Children = null,
    IReadOnlyList<string?>? HiddenColumns = null,
    IReadOnlyList<PortfolioTableColumnRequest?>? Columns = null,
    bool? ShowTotals = null,
    PortfolioTextBoxRequest? Text = null,
    PortfolioMapOptionsRequest? Map = null,
    PortfolioRecordSource? Source = null,
    string? ColumnField = null)
{
    internal void Validate(string at, List<FieldError> errors)
    {
        RequestValidation.Label(Title, $"{at}.title", errors);
        Require(VisualType, $"{at}.visualType", errors);
        Require(GroupBy, $"{at}.groupBy", errors);
        Require(LayoutColumn, $"{at}.layoutColumn", errors);
        Require(LayoutRow, $"{at}.layoutRow", errors);
        Require(LayoutWidth, $"{at}.layoutWidth", errors);
        Require(LayoutHeight, $"{at}.layoutHeight", errors);
        if (Measures is null && Section is null && Values is null && Columns is null && VisualType is not (PortfolioVisualType.Container or PortfolioVisualType.Text))
        {
            errors.Add(new FieldError($"{at}.measures", FieldError.Required));
        }

        for (int i = 0; i < (Values?.Count ?? 0); i++)
        {
            if (Values![i] is not { Field: not null, Aggregation: not null })
            {
                errors.Add(new FieldError($"{at}.values[{i}]", FieldError.Required));
            }
        }

        for (int i = 0; i < (Filters?.Count ?? 0); i++)
        {
            if (Filters![i] is not { Field: not null, Values: not null } filter || filter.Values.Any(v => v is null))
            {
                errors.Add(new FieldError($"{at}.filters[{i}]", FieldError.Required));
            }
        }

        for (int i = 0; i < (HiddenColumns?.Count ?? 0); i++)
        {
            if (HiddenColumns![i] is null)
            {
                errors.Add(new FieldError($"{at}.hiddenColumns[{i}]", FieldError.Required));
            }
        }

        for (int i = 0; i < (Columns?.Count ?? 0); i++)
        {
            if (Columns![i] is not { Kind: not null, Key: not null })
            {
                errors.Add(new FieldError($"{at}.columns[{i}]", FieldError.Required));
            }
        }

        Axes?.Validate($"{at}.axes", errors);
        Text?.Validate($"{at}.text", errors);
        for (int i = 0; i < (Children?.Count ?? 0); i++)
        {
            if (Children![i] is { } child)
            {
                child.Validate($"{at}.children[{i}]", errors);
            }
            else
            {
                errors.Add(new FieldError($"{at}.children[{i}]", FieldError.Required));
            }
        }
    }

    internal PortfolioVisualInput ToInput() =>
        new(Title!.ToLabel(), VisualType!.Value, Measures ?? [], GroupBy!.Value, LayoutColumn!.Value, LayoutRow!.Value, LayoutWidth!.Value, LayoutHeight!.Value, Section,
            Values?.Select(v => new PortfolioFieldValue(v!.Field!, v.Aggregation!.Value)).ToList(), GroupField,
            Filters?.Select(f => new PortfolioFieldFilter(f!.Field!, f.Values!)).ToList(), GroupDateUnit, Axes?.ToAxes(), WidgetCode,
            Children?.Select(c => c!.ToInput()).ToList(), HiddenColumns?.Select(c => c!).ToList(),
            Columns?.Select(c => new PortfolioTableColumn(c!.Kind!.Value, c.Key!, c.Aggregation)).ToList(), ShowTotals, Text?.ToTextBox(),
            Map?.ToOptions(), Source, ColumnField);

    private static void Require<T>(T? value, string field, List<FieldError> errors)
        where T : struct
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
    }
}

/// <summary>
/// What a TEXT visual says (the 20 Nov 2026 text box): its words in both languages as a visual's title is (R-17: present, not blank, each in its own
/// language), its line breaks kept and no other control character; its size, alignment and bold. Its formatting (the 20 Nov 2026 text formatting) is optional,
/// each absent or null property its default: no custom font size, no italic or underline, the theme's text and border colours, no background, no border, at
/// the top. Whether the visual is a text box, the trimmed length (2000), the font size's range and the colours' shape are checked by the service.
/// </summary>
public sealed record PortfolioTextBoxRequest(
    string? Ar,
    string? En,
    PortfolioTextSize? Size,
    PortfolioTextAlign? Align,
    bool? Bold,
    decimal? FontSize = null,
    bool? Italic = null,
    bool? Underline = null,
    string? Color = null,
    string? Background = null,
    bool? Border = null,
    string? BorderColor = null,
    PortfolioTextVerticalAlign? VerticalAlign = null)
{
    /// <summary>What the request may carry in each language before trimming: twice the trimmed limit, as a title's 200 to its 100.</summary>
    private const int MaxLength = 2 * PortfolioTextBox.MaxLength;

    internal void Validate(string at, List<FieldError> errors)
    {
        Words(Ar, $"{at}.ar", TextScript.Arabic, errors);
        Words(En, $"{at}.en", TextScript.English, errors);
        if (Size is null)
        {
            errors.Add(new FieldError($"{at}.size", FieldError.Required));
        }

        if (Align is null)
        {
            errors.Add(new FieldError($"{at}.align", FieldError.Required));
        }

        if (Bold is null)
        {
            errors.Add(new FieldError($"{at}.bold", FieldError.Required));
        }
    }

    internal PortfolioTextBox ToTextBox() =>
        new(
            Ar!, En!, Size!.Value, Align!.Value, Bold!.Value, FontSize, Italic ?? false, Underline ?? false, Color, Background ?? PortfolioTextBox.NoBackground,
            Border ?? false, BorderColor, VerticalAlign ?? PortfolioTextVerticalAlign.Top);

    /// <summary>A title's rules, and no control character but the line break (a tab or carriage return is refused; every request already refuses the rest).</summary>
    private static void Words(string? value, string field, TextScript script, List<FieldError> errors)
    {
        int before = errors.Count;
        RequestValidation.RequireText(value, field, MaxLength, script, errors);
        if (errors.Count == before && value!.Any(c => char.IsControl(c) && c != '\n'))
        {
            errors.Add(new FieldError(field, FieldError.InvalidCharacter));
        }
    }
}

/// <summary>
/// How a MAP visual is drawn (the 20 Nov 2026 map visual): the list of regions and cities beside the map, and the number on each marker; each absent or null
/// property is shown (true). Whether the visual is a map is checked by the service.
/// </summary>
public sealed record PortfolioMapOptionsRequest(bool? ShowList = null, bool? ShowCounts = null)
{
    internal PortfolioMapOptions ToOptions() => new(ShowList ?? true, ShowCounts ?? true);
}

/// <summary>A value a visual draws: a field key of the data catalogue and its aggregation; how they fit the visual is checked by the service.</summary>
public sealed record PortfolioFieldValueRequest(string? Field, PortfolioFieldAggregation? Aggregation);

/// <summary>
/// A column of a TABLE visual (the 20 Nov 2026 Power BI-style table): its kind, its key and its calculation (null for a measure); whether they fit is checked
/// by the service.
/// </summary>
public sealed record PortfolioTableColumnRequest(PortfolioTableColumnKind? Kind, string? Key, PortfolioTableAggregation? Aggregation = null);

/// <summary>A visual's filter: a CODE field key or a dimension key, and the values that pass; their shape is checked by the service.</summary>
public sealed record PortfolioFieldFilterRequest(string? Field, IReadOnlyList<string>? Values);

/// <summary>
/// How a bar, column or line chart draws its axes (the 19 Nov 2026 axes): both axes, each saying whether it is shown; a title, when given, in both languages.
/// Which types take axes, the titles' length and the range are checked by the service.
/// </summary>
public sealed record PortfolioAxesRequest(PortfolioAxisRequest? Category, PortfolioValueAxisRequest? Value)
{
    internal void Validate(string at, List<FieldError> errors)
    {
        if (Category is null)
        {
            errors.Add(new FieldError($"{at}.category", FieldError.Required));
        }
        else
        {
            RequireFlag(Category.Show, $"{at}.category.show", errors);
            OptionalTitle(Category.Title, $"{at}.category.title", errors);
        }

        if (Value is null)
        {
            errors.Add(new FieldError($"{at}.value", FieldError.Required));
        }
        else
        {
            RequireFlag(Value.Show, $"{at}.value.show", errors);
            RequireFlag(Value.Gridlines, $"{at}.value.gridlines", errors);
            OptionalTitle(Value.Title, $"{at}.value.title", errors);
        }
    }

    internal PortfolioAxes ToAxes() =>
        new(new PortfolioAxis(Category!.Show!.Value, Category.Title?.ToLabel()),
            new PortfolioValueAxis(Value!.Show!.Value, Value.Title?.ToLabel(), Value.Min, Value.Max, Value.Gridlines!.Value));

    private static void RequireFlag(bool? value, string field, List<FieldError> errors)
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
    }

    private static void OptionalTitle(BilingualLabelRequest? title, string field, List<FieldError> errors)
    {
        if (title is not null)
        {
            RequestValidation.Label(title, field, errors);
        }
    }
}

/// <summary>The category axis: shown or not, and its title (null for none).</summary>
public sealed record PortfolioAxisRequest(bool? Show, BilingualLabelRequest? Title = null);

/// <summary>The value axis: shown or not, its title (null for none), its start and end (null = automatic) and whether gridlines are drawn.</summary>
public sealed record PortfolioValueAxisRequest(bool? Show, bool? Gridlines, BilingualLabelRequest? Title = null, decimal? Min = null, decimal? Max = null);
