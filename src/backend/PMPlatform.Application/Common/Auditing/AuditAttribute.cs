using System.Diagnostics.CodeAnalysis;

namespace PMPlatform.Application.Common.Auditing;

/// <summary>
/// A named value captured with an audit event: the old and new value of a change, or a fact about the occurrence (only
/// <see cref="NewValue"/>). Values are formatted by <see cref="AuditValue"/>.
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "The ERD's term: each becomes an audit_activity.audit_event_attribute row.")]
public sealed record AuditAttribute(string Name, string? OldValue, string? NewValue)
{
    /// <summary>
    /// Stands for a personal value that changed but is not copied into the audit store or the SIEM: an email address or
    /// mobile number. The event still records which field changed.
    /// </summary>
    public const string Withheld = "[WITHHELD]";

    /// <summary>A fact about the occurrence.</summary>
    public static AuditAttribute Of(string name, object? value) => new(name, null, AuditValue.Format(value));

    /// <summary>A change, or null if the value did not change.</summary>
    public static AuditAttribute? Change(string name, object? oldValue, object? newValue)
    {
        string? before = AuditValue.Format(oldValue);
        string? after = AuditValue.Format(newValue);
        return string.Equals(before, after, StringComparison.Ordinal) ? null : new AuditAttribute(name, before, after);
    }

    /// <summary>A change to a personal value, recorded without the value; null if the value did not change.</summary>
    public static AuditAttribute? WithheldChange(string name, string? oldValue, string? newValue) =>
        string.Equals(oldValue, newValue, StringComparison.Ordinal)
            ? null
            : new AuditAttribute(name, oldValue is null ? null : Withheld, newValue is null ? null : Withheld);
}
