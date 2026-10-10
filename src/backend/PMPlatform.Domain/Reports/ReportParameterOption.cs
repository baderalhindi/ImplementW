using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A fixed option of an OPTION parameter: a value of the field the parameter filters, and the FG-02 catalogue entry it absorbs (ADR-006 MAPPED),
/// e.g. the SUSPENDED option of the Project Register is RPT-SUS-001. Delete policy: CASCADE, while the version is a DRAFT.
/// </summary>
public sealed class ReportParameterOption : AuditedEntity
{
    public Guid ReportParameterId { get; set; }

    public required string ValueCode { get; set; }

    public required BilingualLabel Label { get; set; }

    public string? CatalogueEntryReference { get; set; }
}
