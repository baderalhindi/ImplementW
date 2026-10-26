namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// How many SMS segments a text costs (3GPP TS 23.038/23.040). Text entirely in the GSM 7-bit default alphabet is 160
/// septets in one segment or 153 per segment when concatenated, an extension-table character costing two septets. Any
/// other character — every Arabic letter — puts the whole text in UCS-2: 70 characters in one segment, 67 per segment
/// when concatenated (ADR-004's Arabic segment budget).
/// </summary>
internal static class SmsSegments
{
    private const string Basic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    private const string Extension = "\f^{}\\[~]|€";

    public static int Count(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int septets = 0;
        foreach (char c in text)
        {
            if (Basic.Contains(c, StringComparison.Ordinal))
            {
                septets++;
            }
            else if (Extension.Contains(c, StringComparison.Ordinal))
            {
                septets += 2;
            }
            else
            {
                return Segments(text.Length, single: 70, concatenated: 67);
            }
        }

        return Segments(septets, single: 160, concatenated: 153);
    }

    private static int Segments(int units, int single, int concatenated) =>
        units <= single ? 1 : (units + concatenated - 1) / concatenated;
}
