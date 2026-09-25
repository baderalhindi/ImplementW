using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>Whether a contribution type is enabled in a participation mode (ADR-013, TASK-034). Delete policy: CASCADE.</summary>
public sealed class ParticipationContributionRule : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public ParticipationMode ParticipationMode { get; set; }

    /// <summary>Master data item of catalogue CONTRIBUTION_TYPE.</summary>
    public Guid ContributionTypeItemId { get; set; }

    public bool IsEnabled { get; set; }
}
