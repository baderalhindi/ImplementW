using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// SHA-256 over an event's attributes, stored on the event and covered by its chained hash (TASK-073; TASK-033 F-2). The
/// attributes are taken in ordinal order of name; each contributes its name, old value and new value, each written as
/// <c>-</c> when null or as <c>+&lt;UTF-8 byte length&gt;:&lt;value&gt;</c>, so no two different sets of attributes are written
/// alike. Lower-case hex, as PostgreSQL's <c>encode(…, 'hex')</c> writes the event hash.
/// </summary>
public static class AuditAttributeDigest
{
    public static string Of(IEnumerable<AuditEventAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        return Of(attributes.Select(a => (a.AttributeName, a.OldValue, a.NewValue)));
    }

    public static string Of(IEnumerable<(string Name, string? OldValue, string? NewValue)> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        StringBuilder canonical = new();
        foreach ((string name, string? oldValue, string? newValue) in attributes.OrderBy(a => a.Name, StringComparer.Ordinal))
        {
            Append(canonical, name);
            Append(canonical, oldValue);
            Append(canonical, newValue);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder canonical, string? value)
    {
        if (value is null)
        {
            canonical.Append('-');
            return;
        }

        canonical.Append('+').Append(Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
}
