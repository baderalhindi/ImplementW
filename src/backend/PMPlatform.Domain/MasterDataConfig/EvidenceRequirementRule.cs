using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>The evidence a milestone category requires (PTBC-006, PTBC-019; TASK-051). Delete policy: CASCADE.</summary>
public sealed class EvidenceRequirementRule : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public Guid MilestoneCategoryItemId { get; set; }

    public Guid EvidenceTypeItemId { get; set; }

    public bool IsMandatory { get; set; }
}
