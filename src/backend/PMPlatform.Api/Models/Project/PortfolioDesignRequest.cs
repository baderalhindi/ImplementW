using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Models.Project;

/// <summary>
/// The Portfolio dashboard's design as a whole, in reading order (the 17 Nov 2026 designer). Each field is required here; how they fit together — a grouping
/// the type takes, a place on the grid no other visual holds — is checked by the service (422 <c>PROJECT_PORTFOLIO_DESIGN_INVALID</c>).
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
        else if (Visuals.Count > PortfolioDesignInput.MaxVisuals)
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
    short? LayoutColumn,
    short? LayoutRow,
    short? LayoutWidth,
    short? LayoutHeight,
    PortfolioSection? Section = null,
    IReadOnlyList<PortfolioFieldValueRequest?>? Values = null,
    string? GroupField = null,
    IReadOnlyList<PortfolioFieldFilterRequest?>? Filters = null)
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
        if (Measures is null && Section is null && Values is null)
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
    }

    internal PortfolioVisualInput ToInput() =>
        new(Title!.ToLabel(), VisualType!.Value, Measures ?? [], GroupBy!.Value, LayoutColumn!.Value, LayoutRow!.Value, LayoutWidth!.Value, LayoutHeight!.Value, Section,
            Values?.Select(v => new PortfolioFieldValue(v!.Field!, v.Aggregation!.Value)).ToList(), GroupField,
            Filters?.Select(f => new PortfolioFieldFilter(f!.Field!, f.Values!)).ToList());

    private static void Require<T>(T? value, string field, List<FieldError> errors)
        where T : struct
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
    }
}

/// <summary>A value a visual draws: a field key of the data catalogue and its aggregation; how they fit the visual is checked by the service.</summary>
public sealed record PortfolioFieldValueRequest(string? Field, PortfolioFieldAggregation? Aggregation);

/// <summary>A visual's filter: a CODE field key or a dimension key, and the values that pass; their shape is checked by the service.</summary>
public sealed record PortfolioFieldFilterRequest(string? Field, IReadOnlyList<string>? Values);
