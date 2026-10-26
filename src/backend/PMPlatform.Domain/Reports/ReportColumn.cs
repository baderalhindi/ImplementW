using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A column a report version publishes (FG-02 REP ReportColumnDefinition): a field of the row's project (<c>PROJECT</c>) or of a registered
/// projection, named as configuration names it — the projection's code without its dot — and its label. A person chooses among a report's
/// columns only (FG-02 §6.2). Delete policy: CASCADE, while the version is a DRAFT.
/// </summary>
public sealed class ReportColumn : AuditedEntity
{
    public Guid ReportDefinitionId { get; set; }

    public required string SourceEntityCode { get; set; }

    public required string FieldCode { get; set; }

    public required BilingualLabel Label { get; set; }

    /// <summary>The column's place in the report, from 1.</summary>
    public short SortOrder { get; set; }

    /// <summary>Shown when no column choice is made (REP-010).</summary>
    public bool IsDefaultVisible { get; set; }

    /// <summary>A DATA_CLASSIFICATION item a viewer must be cleared for to see the column's values (ADR-010); null when unclassified.</summary>
    public Guid? DataClassificationItemId { get; set; }
}
