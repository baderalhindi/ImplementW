using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.DataImport;

/// <summary>
/// One worksheet row whose every cell has the shape its column requires, typed: a register reads it without parsing. An empty cell
/// reads as null; a required column is never null.
/// </summary>
public sealed class ImportRecord(int rowNumber, string legacyId, IReadOnlyDictionary<string, object> values)
{
    /// <summary>The worksheet row, 1-based, as the spreadsheet numbers it.</summary>
    public int RowNumber { get; } = rowNumber;

    public string LegacyId { get; } = legacyId;

    public string? Text(string key) => values.TryGetValue(key, out object? value) ? (string)value : null;

    public DateOnly? Date(string key) => Value<DateOnly>(key);

    public decimal? Number(string key) => Value<decimal>(key);

    public int? WholeNumber(string key) => Value<int>(key);

    public bool? Boolean(string key) => Value<bool>(key);

    public Money? Money(string key) => Value<Money>(key);

    /// <summary>A choice column built by <see cref="ImportColumn.ChoiceOf{TEnum}"/>, as its enum value.</summary>
    public TEnum? Choice<TEnum>(string key)
        where TEnum : struct, Enum =>
        Text(key) is { } text ? Enum.GetValues<TEnum>().Single(v => ImportColumn.ChoiceText(v) == text) : null;

    private T? Value<T>(string key)
        where T : struct =>
        values.TryGetValue(key, out object? value) ? (T)value : null;
}
