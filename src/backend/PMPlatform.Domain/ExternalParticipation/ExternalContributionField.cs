using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// One value of a contribution revision, for one field of its typed schema (TASK-066). The value is stored as text in the schema's
/// canonical form — a number in invariant notation, a date as ISO 8601 — and a narrative carries its entry language. Written while the
/// revision is a DRAFT, never after. Delete policy: CASCADE.
/// </summary>
public sealed class ExternalContributionField : AuditedEntity
{
    public Guid ExternalContributionId { get; set; }

    /// <summary>The field's code in the contribution type's schema: only allowlisted codes are ever stored (WF-13 BR-EXT-008).</summary>
    public required string FieldCode { get; set; }

    public required string ProposedValue { get; set; }

    /// <summary>The entry language of a narrative value; null for any other type (ERD D-7).</summary>
    public Language? ProposedValueLanguage { get; set; }
}
