namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// SR-38 (DOC-T01): what each accepted media type must look like. A file is accepted only when its name ends in one of the
/// type's extensions and its leading bytes are the type's signature. A media type with no rule here is refused, whatever the
/// upload policy lists, and the start-up check refuses such a policy.
/// </summary>
internal static class DocumentContentRules
{
    /// <summary>How many leading bytes are read to judge a file: the signatures need 8; text is judged on this whole block.</summary>
    public const int HeaderLength = 8192;

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] WindowsExecutable = "MZ"u8.ToArray();
    private static readonly byte[] Elf = [0x7F, 0x45, 0x4C, 0x46];

    /// <summary>Signatures a text file may not start with: every binary format above, and executables.</summary>
    private static readonly byte[][] BinarySignatures = [Pdf, Zip, Ole, Png, Jpeg, WindowsExecutable, Elf];

    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = new([".pdf"], Pdf),
        ["application/msword"] = new([".doc"], Ole),
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new([".docx"], Zip),
        ["application/vnd.ms-excel"] = new([".xls"], Ole),
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = new([".xlsx"], Zip),
        ["application/vnd.ms-powerpoint"] = new([".ppt"], Ole),
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = new([".pptx"], Zip),
        ["image/png"] = new([".png"], Png),
        ["image/jpeg"] = new([".jpg", ".jpeg"], Jpeg),
        ["text/plain"] = new([".txt"], null),
        ["text/csv"] = new([".csv"], null),
    };

    /// <summary>Whether the platform can verify files of this media type (no parameters).</summary>
    public static bool IsKnown(string mediaType) => Rules.ContainsKey(mediaType);

    /// <summary>Whether a file of the given name may be of this media type (no parameters); the extension is compared ignoring case.</summary>
    public static bool ExtensionMatches(string mediaType, string fileName) =>
        Rules.TryGetValue(mediaType, out Rule? rule)
        && rule.Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="header"/>, the file's first <see cref="HeaderLength"/> bytes (or all of it, if shorter), is of
    /// this media type: the type's signature, or for text no NUL byte and no binary signature at the start.
    /// </summary>
    public static bool ContentMatches(string mediaType, ReadOnlySpan<byte> header)
    {
        if (!Rules.TryGetValue(mediaType, out Rule? rule))
        {
            return false;
        }

        if (rule.Signature is { } signature)
        {
            return header.StartsWith(signature);
        }

        if (header.IsEmpty || header.Contains((byte)0))
        {
            return false;
        }

        foreach (byte[] binary in BinarySignatures)
        {
            if (header.StartsWith(binary))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A type's extensions, and its leading bytes; a null signature means text.</summary>
    private sealed record Rule(string[] Extensions, byte[]? Signature);
}
