using System.Globalization;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.DataImport;

/// <summary>
/// A cell's text as its column's type, or the validation code it breaks. Every cell obeys <see cref="TextInput"/>, as text typed into
/// a form does (TASK-077); leading and trailing white space is dropped, as a spreadsheet user does not see it.
/// </summary>
internal static class ImportCellParser
{
    private const string IsoDate = "yyyy-MM-dd";

    // A spreadsheet's date serials, which agree with OLE Automation dates from 61 (1900-03-01, after the spreadsheet's phantom
    // 1900-02-29) to 2958465 (9999-12-31).
    private const double FirstSerial = 61;
    private const double LastSerial = 2_958_465;

    // numeric(18,2) (ERD D-5).
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;

    /// <summary>The typed value, or null with <paramref name="issue"/> set; null with no issue is an empty cell.</summary>
    public static object? Parse(ImportColumn column, string? cell, out string? issue)
    {
        ArgumentNullException.ThrowIfNull(column);
        issue = null;
        string text = cell?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            issue = column.Required ? ValidationCode.Required : null;
            return null;
        }

        if (TextInput.Violation(text) is { } violation)
        {
            issue = violation;
            return null;
        }

        object? value = column.Kind switch
        {
            ImportValueKind.Text => text.Length <= column.MaxLength ? text : Refuse(ValidationCode.MaxLength, out issue),
            ImportValueKind.Code => text.Length > column.MaxLength ? Refuse(ValidationCode.MaxLength, out issue)
                : TextShape.IsCode(text) ? text : Refuse(ValidationCode.Malformed, out issue),
            ImportValueKind.Email => text.Length > column.MaxLength ? Refuse(ValidationCode.MaxLength, out issue)
                : TextShape.IsEmail(text) ? text : Refuse(ValidationCode.Malformed, out issue),
            ImportValueKind.PhoneNumber => TextShape.IsE164(text) ? text : Refuse(ValidationCode.Malformed, out issue),
            ImportValueKind.Date => ParseDate(text, out issue),
            ImportValueKind.Number => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)
                ? number : Refuse(ValidationCode.Malformed, out issue),
            ImportValueKind.WholeNumber => ParseInteger(text, out issue),
            ImportValueKind.Boolean => text.ToUpperInvariant() switch
            {
                "TRUE" => true,
                "FALSE" => false,
                _ => Refuse(ValidationCode.Malformed, out issue),
            },
            ImportValueKind.Choice => column.Choices.FirstOrDefault(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase))
                ?? Refuse(ValidationCode.EnumValue, out issue),
            ImportValueKind.Money => ParseMoney(text, out issue),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column.Kind, "Unknown import value kind."),
        };
        return value;
    }

    /// <summary>ISO 8601 as typed into a text cell, or the serial number a date cell holds.</summary>
    private static object? ParseDate(string text, out string? issue)
    {
        issue = null;
        return DateOnly.TryParseExact(text, IsoDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial)
              && serial is >= FirstSerial and <= LastSerial && serial == Math.Floor(serial)
                ? DateOnly.FromDateTime(DateTime.FromOADate(serial))
                : Refuse(ValidationCode.Malformed, out issue);
    }

    /// <summary>A whole number; a number cell holding one may carry a zero fraction.</summary>
    private static object? ParseInteger(string text, out string? issue)
    {
        issue = null;
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)
               && number == decimal.Truncate(number) && number is >= int.MinValue and <= int.MaxValue
            ? (int)number
            : Refuse(ValidationCode.Malformed, out issue);
    }

    /// <summary>R-16's amount, as a number cell or typed text holds it: two decimal places at most, never negative.</summary>
    private static object? ParseMoney(string text, out string? issue)
    {
        issue = null;
        return !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal amount) || decimal.Round(amount, 2) != amount
            ? Refuse(ValidationCode.Malformed, out issue)
            : amount is < 0 or > MaxMoney ? Refuse(ValidationCode.OutOfRange, out issue) : new Money(amount);
    }

    private static object? Refuse(string code, out string? issue)
    {
        issue = code;
        return null;
    }
}
