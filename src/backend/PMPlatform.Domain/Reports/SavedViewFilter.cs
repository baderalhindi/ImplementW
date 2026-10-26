using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A filter of an SCR-138 composition: an allowlisted filterable field, an operator from the closed set and a value, never an expression
/// (FG-02 §6.1). Delete policy: CASCADE.
/// </summary>
public sealed class SavedViewFilter : AuditedEntity
{
    public Guid SavedViewId { get; set; }

    public Guid ReportAllowlistEntryId { get; set; }

    public ReportFilterOperator Operator { get; set; }

    /// <summary>The value as entered: one value, two for BETWEEN, several for IN, separated by commas.</summary>
    public required string ValueText { get; set; }
}
