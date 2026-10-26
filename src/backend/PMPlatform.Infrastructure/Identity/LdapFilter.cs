using System.Globalization;
using System.Text;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>RFC 4515 assertion-value escaping, so a username can never change the structure of a search filter.</summary>
internal static class LdapFilter
{
    public static string Equality(string attribute, string value) => $"({attribute}={Escape(value)})";

    /// <summary>An equality filter on a binary attribute, every byte written as <c>\hh</c>.</summary>
    public static string Equality(string attribute, byte[] value) =>
        $"({attribute}={string.Concat(value.Select(b => string.Create(CultureInfo.InvariantCulture, $"\\{b:x2}")))})";

    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder escaped = new(value.Length);
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            // NUL, '(', ')', '*', '\' must be escaped; every other non-ASCII or control byte is escaped too.
            if (b is 0x00 or (byte)'(' or (byte)')' or (byte)'*' or (byte)'\\' or < 0x20 or > 0x7e)
            {
                escaped.Append(CultureInfo.InvariantCulture, $"\\{b:x2}");
            }
            else
            {
                escaped.Append((char)b);
            }
        }

        return escaped.ToString();
    }
}
