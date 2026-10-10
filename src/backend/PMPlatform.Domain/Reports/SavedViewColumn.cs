using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A column of an SCR-138 composition (MOD-061). It names an entry of the REPORT_RULES allowlist and nothing else: the foreign key is the
/// allowlist's enforcement in the database (ERD §5.20). A sorted column carries its direction; columns sort in their order. Delete policy: CASCADE.
/// </summary>
public sealed class SavedViewColumn : AuditedEntity
{
    public Guid SavedViewId { get; set; }

    public Guid ReportAllowlistEntryId { get; set; }

    /// <summary>The column's place, from 1.</summary>
    public short SortOrder { get; set; }

    public ReportSortDirection? SortDirection { get; set; }
}
