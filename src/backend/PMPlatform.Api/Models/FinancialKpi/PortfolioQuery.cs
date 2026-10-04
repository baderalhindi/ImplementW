using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.FinancialKpi;

/// <summary>A portfolio named in the query string: one to 200 ids, repeated as <c>name=…&amp;name=…</c>.</summary>
internal static class PortfolioQuery
{
    public const int MaxSize = 200;

    public static void RequireIds(Guid[]? ids, string field, List<FieldError> errors)
    {
        if (ids is null || ids.Length == 0 || ids.Contains(Guid.Empty))
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
        else if (ids.Length > MaxSize)
        {
            errors.Add(new FieldError(field, FieldError.OutOfRange));
        }
    }
}
