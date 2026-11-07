using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A project lifecycle stage and its weight, inside a PROJECT_STAGE version (review round 5, item 7). A stage once published is deactivated, never removed.</summary>
public sealed class ProjectStageDefinition : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public short SortOrder { get; set; }

    /// <summary>The active stages' weights total 100; a deactivated stage carries none.</summary>
    public decimal WeightPercent { get; set; }

    public bool IsActive { get; set; } = true;
}
