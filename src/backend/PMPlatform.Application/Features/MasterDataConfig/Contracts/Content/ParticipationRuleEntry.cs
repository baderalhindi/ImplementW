using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>Whether a contribution type is enabled for projects in a participation mode (ADR-013).</summary>
public sealed record ParticipationRuleEntry(ParticipationMode ParticipationMode, Guid ContributionTypeItemId, bool IsEnabled);
