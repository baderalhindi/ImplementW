using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>A parameter value of a saved REPORT_PARAMETERS view (SAV-006), revalidated at every use. Delete policy: CASCADE.</summary>
public sealed class ReportParameterValue : AuditedEntity
{
    public Guid SavedViewId { get; set; }

    public required string ParameterCode { get; set; }

    public required string ValueText { get; set; }
}
