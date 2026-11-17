using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
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
    short? LayoutHeight)
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
        if (Measures is null)
        {
            errors.Add(new FieldError($"{at}.measures", FieldError.Required));
        }
    }

    internal PortfolioVisualInput ToInput() =>
        new(Title!.ToLabel(), VisualType!.Value, Measures!, GroupBy!.Value, LayoutColumn!.Value, LayoutRow!.Value, LayoutWidth!.Value, LayoutHeight!.Value);

    private static void Require<T>(T? value, string field, List<FieldError> errors)
        where T : struct
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
    }
}
