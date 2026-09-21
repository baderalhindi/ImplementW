namespace PMPlatform.Domain.Common;

/// <summary>
/// A controlled label held in both languages (ADR-012 Option B; ERD D-6). Maps to a <c>&lt;name&gt;_ar</c> /
/// <c>&lt;name&gt;_en</c> column pair with the same nullability. Both values are always present.
/// </summary>
public sealed record BilingualLabel
{
    public string Ar { get; }

    public string En { get; }

    public BilingualLabel(string ar, string en)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ar);
        ArgumentException.ThrowIfNullOrWhiteSpace(en);
        Ar = ar;
        En = en;
    }

    public string In(Language language) => language == Language.Ar ? Ar : En;
}
