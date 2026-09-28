using System.Globalization;
using System.Text.Json;

namespace PMPlatform.Application.Common.Auditing;

/// <summary>
/// How a value is written into the audit store and the SIEM: an enumeration in upper snake case, as the API and the
/// database write it (api-conventions R-19, ERD §6); a uuid in its hyphenated form; a time in ISO 8601 UTC.
/// </summary>
public static class AuditValue
{
    public static string? Format(object? value) => value switch
    {
        null => null,
        string text => text,
        Enum member => JsonNamingPolicy.SnakeCaseUpper.ConvertName(member.ToString()),
        Guid id => id.ToString("D"),
        DateTimeOffset time => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
