namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// ERD <c>financial_source_mode.source_mode</c> (ADR-008 gate): MANUAL at launch; INTEGRATED and HYBRID are built and left
/// unconnected. A field that is INTEGRATED takes no manual figure, and never goes back to MANUAL: manual substitution during
/// a source outage is not permitted.
/// </summary>
public enum SourceMode
{
    Manual = 1,
    Integrated = 2,
    Hybrid = 3,
}
