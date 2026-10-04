using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// The source mode of one financial field of one project (ADR-008: "source mode set per project and per field"). A field with
/// no row is MANUAL, the launch mode. Delete policy: RETAIN.
/// </summary>
public sealed class FinancialSourceMode : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public FinancialField FieldCode { get; set; }

    public SourceMode SourceMode { get; set; }

    /// <summary>When the mode in force was set.</summary>
    public DateTimeOffset ConfiguredAt { get; set; }
}
