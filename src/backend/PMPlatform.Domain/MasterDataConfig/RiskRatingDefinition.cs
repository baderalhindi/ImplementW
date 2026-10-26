using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A rating label, e.g. Low, Medium, High, Critical, inside a risk matrix version. Delete policy: CASCADE.</summary>
public sealed class RiskRatingDefinition : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public required string Code { get; set; }

    public required BilingualLabel Label { get; set; }

    public short SortOrder { get; set; }
}
