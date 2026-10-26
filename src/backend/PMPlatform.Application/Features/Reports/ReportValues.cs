using System.Globalization;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// A field's values as typed values: what a filter or a parameter may say (a value of the field's type and, for a code, of its closed set), how a
/// cell is compared with it, and how cells are ordered. A cell with no value — missing, not applicable, restricted, unavailable — matches no
/// filter, so a filter can never be used to learn what the caller may not see (FG-02 §8.2), and sorts after every value.
/// </summary>
internal static class ReportValues
{
    public const int MaxValueLength = 500;

    public const int MaxListValues = 50;

    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>The values a filter's text holds for its operator: one, two for BETWEEN, up to fifty for IN; null when the count is wrong.</summary>
    public static IReadOnlyList<string>? Split(ReportFilterOperator op, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (op is not (ReportFilterOperator.In or ReportFilterOperator.Between))
        {
            return [text];
        }

        string[] values = [.. text.Split(',').Select(v => v.Trim())];
        return op == ReportFilterOperator.Between ? values.Length == 2 ? values : null
            : values.Length is >= 1 and <= MaxListValues ? values : null;
    }

    /// <summary>Whether <paramref name="text"/> is a value of <paramref name="field"/>, and its canonical form.</summary>
    public static bool TryParse(ReportField field, string? text, out string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        value = string.Empty;
        if (string.IsNullOrEmpty(text) || text.Length > MaxValueLength || text.Any(char.IsControl))
        {
            return false;
        }

        switch (field.Type)
        {
            case ReportValueType.Code:
                value = text;
                return field.Values.Contains(text, StringComparer.Ordinal);
            case ReportValueType.Text:
                value = text;
                return true;
            case ReportValueType.Reference:
                bool isId = Guid.TryParse(text, out Guid id);
                value = id.ToString();
                return isId;
            case ReportValueType.Count or ReportValueType.Days:
                bool isInteger = int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int integer);
                value = integer.ToString(CultureInfo.InvariantCulture);
                return isInteger;
            case ReportValueType.Percent or ReportValueType.Sar:
                bool isDecimal = decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number);
                value = number.ToString(CultureInfo.InvariantCulture);
                return isDecimal;
            case ReportValueType.Boolean:
                value = text;
                return text is "true" or "false";
            case ReportValueType.Date:
                bool isDate = DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date);
                value = date.ToString(DateFormat, CultureInfo.InvariantCulture);
                return isDate;
            case ReportValueType.DateTime:
                bool isTime = DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at);
                value = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
                return isTime;
            default:
                throw new InvalidOperationException($"{field.Key} has an unknown type.");
        }
    }

    /// <summary>Whether a cell satisfies a filter. Only a value the caller is shown can match.</summary>
    public static bool Matches(ReportField field, ReportCell cell, ReportFilterOperator op, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(values);
        return cell.Value is { } actual && cell.UnknownReason is null && !cell.IsMasked && op switch
        {
            ReportFilterOperator.Eq => Compare(field, actual, values[0]) == 0,
            ReportFilterOperator.Neq => Compare(field, actual, values[0]) != 0,
            ReportFilterOperator.In => values.Any(v => Compare(field, actual, v) == 0),
            ReportFilterOperator.Gt => Compare(field, actual, values[0]) > 0,
            ReportFilterOperator.Gte => Compare(field, actual, values[0]) >= 0,
            ReportFilterOperator.Lt => Compare(field, actual, values[0]) < 0,
            ReportFilterOperator.Lte => Compare(field, actual, values[0]) <= 0,
            ReportFilterOperator.Between => Compare(field, actual, values[0]) >= 0 && Compare(field, actual, values[1]) <= 0,
            ReportFilterOperator.Contains => actual.Contains(values[0], StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown operator."),
        };
    }

    /// <summary>
    /// Orders two cells of a field: values by type — a code by its place in the source's own order of states, never a priority invented here
    /// (US-RPT-PMO-013) — and every cell without a value after every value.
    /// </summary>
    public static int CompareCells(ReportField field, ReportCell a, ReportCell b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        bool hasA = a.UnknownReason is null && a.Value is not null;
        bool hasB = b.UnknownReason is null && b.Value is not null;
        return hasA && hasB ? Compare(field, a.Value!, b.Value!) : hasA ? -1 : hasB ? 1 : 0;
    }

    private static int Compare(ReportField field, string a, string b) => field.Type switch
    {
        ReportValueType.Code => Rank(field, a).CompareTo(Rank(field, b)),
        ReportValueType.Text => StringComparer.OrdinalIgnoreCase.Compare(a, b) is var c and not 0 ? c : string.CompareOrdinal(a, b),
        ReportValueType.Reference or ReportValueType.Boolean => string.CompareOrdinal(a, b),
        ReportValueType.Count or ReportValueType.Days or ReportValueType.Percent or ReportValueType.Sar =>
            decimal.Parse(a, CultureInfo.InvariantCulture).CompareTo(decimal.Parse(b, CultureInfo.InvariantCulture)),
        ReportValueType.Date => DateOnly.ParseExact(a, DateFormat, CultureInfo.InvariantCulture).CompareTo(DateOnly.ParseExact(b, DateFormat, CultureInfo.InvariantCulture)),
        ReportValueType.DateTime => DateTimeOffset.Parse(a, CultureInfo.InvariantCulture).CompareTo(DateTimeOffset.Parse(b, CultureInfo.InvariantCulture)),
        _ => throw new InvalidOperationException($"{field.Key} has an unknown type."),
    };

    private static int Rank(ReportField field, string code) =>
        field.Values.IndexOf(code) is var index and >= 0 ? index : int.MaxValue;

    private static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
