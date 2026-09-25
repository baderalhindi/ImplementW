using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// A row of the approval authority matrix: which internal role approves which subject at which band or value
/// (OQ-005; shape only). Delete policy: CASCADE.
/// </summary>
public sealed class ApprovalAuthorityRule : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    /// <summary>e.g. PROJECT_REGISTRATION, BASELINE, CHANGE_REQUEST, SUSPENSION, COMPLETION, CLOSURE.</summary>
    public required string SubjectTypeCode { get; set; }

    /// <summary>Null applies the rule to every profile.</summary>
    public Guid? GovernanceProfileItemId { get; set; }

    /// <summary>The materiality band, where one applies.</summary>
    public short? BandNo { get; set; }

    public Money? MinAmountSar { get; set; }

    /// <summary>The stage order.</summary>
    public short SequenceNo { get; set; }

    /// <summary>Internal roles only: an external user holds no approval authority (ADR-013).</summary>
    public Guid ApproverRoleId { get; set; }

    public bool IsMandatory { get; set; } = true;
}
