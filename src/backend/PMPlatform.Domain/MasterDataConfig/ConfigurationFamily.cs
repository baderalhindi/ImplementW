using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// A versioned policy configuration family: GOVERNANCE_PROFILE, MATERIALITY_BAND, RISK_MATRIX, APPROVAL_AUTHORITY,
/// NOTIFICATION_ROUTING, KPI_POLICY, PARTICIPATION, EVIDENCE_POLICY, FIELD_CLASSIFICATION, REPORT_RULES,
/// DASHBOARD_RULES, WORKFLOW_POLICY. Delete policy: RETAIN.
/// </summary>
public sealed class ConfigurationFamily : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }
}
