namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>The catalogues configuration content and module records reference by rule (db/seed; seed-data-and-integrity.md §3.1).</summary>
public static class MasterDataCatalogueCodes
{
    public const string GovernanceProfile = "GOVERNANCE_PROFILE";
    public const string ImpactDimension = "IMPACT_DIMENSION";
    public const string RiskCategory = "RISK_CATEGORY";
    public const string ConcernCategory = "CONCERN_CATEGORY";
    public const string ConcernSeverity = "CONCERN_SEVERITY";
    public const string Priority = "PRIORITY";
    public const string DataClassification = "DATA_CLASSIFICATION";
    public const string DocumentControlLevel = "DOCUMENT_CONTROL_LEVEL";
    public const string MilestoneCategory = "MILESTONE_CATEGORY";
    public const string EvidenceType = "EVIDENCE_TYPE";
    public const string DocumentType = "DOCUMENT_TYPE";
    public const string ContributionType = "CONTRIBUTION_TYPE";
    public const string KpiUnit = "KPI_UNIT";
    public const string MeasurementFrequency = "MEASUREMENT_FREQUENCY";
    public const string EtimadCostCategory = "ETIMAD_COST_CATEGORY";
    public const string ProjectClassification = "PROJECT_CLASSIFICATION";
    public const string Region = "REGION";
    public const string City = "CITY";
    public const string ExternalEntityType = "EXTERNAL_ENTITY_TYPE";

    /// <summary>RPMO's strategic portfolios (the 2 Nov 2026 review, R3-3), labelled "المحفظة الاستراتيجية".</summary>
    public const string StrategicPortfolio = "STRATEGIC_PORTFOLIO";

    /// <summary>RPMO's strategic initiatives, each an item under one STRATEGIC_PORTFOLIO item.</summary>
    public const string StrategicInitiative = "STRATEGIC_INITIATIVE";
}
