using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// The sensitivity classification and masking rule of one field (ADR-010; taxonomy and field list outstanding with
/// AHDA Cybersecurity). Delete policy: CASCADE.
/// </summary>
public sealed class FieldClassificationRule : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public required string EntityCode { get; set; }

    public required string FieldCode { get; set; }

    /// <summary>Master data item of catalogue DATA_CLASSIFICATION.</summary>
    public Guid DataClassificationItemId { get; set; }

    public MaskingRule MaskingRule { get; set; }
}
