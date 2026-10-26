using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>The editable representation of a DRAFT version, replaced in full: its change summary and its whole content.</summary>
public sealed record ConfigurationVersionChanges(NarrativeText? ChangeSummary, ConfigurationContent Content);
