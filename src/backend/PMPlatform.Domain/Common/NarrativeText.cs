namespace PMPlatform.Domain.Common;

/// <summary>
/// Free text a person typed, tagged with the language it was entered in (ADR-012 extension, TASK-109; ERD D-7).
/// Maps to a <c>&lt;field&gt; text</c> / <c>&lt;field&gt;_lang char(2)</c> column pair. Nothing is translated:
/// the tag records the entry language and is set once, at entry, because it cannot be backfilled later.
/// </summary>
public sealed record NarrativeText
{
    public string Text { get; }

    public Language Language { get; }

    public NarrativeText(string text, Language language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        Language = language;
    }
}
