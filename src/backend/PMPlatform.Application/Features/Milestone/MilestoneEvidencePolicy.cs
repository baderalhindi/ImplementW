using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Application.Features.Milestone;

/// <summary>The mandatory evidence of an achievement (PTBC-006): EVIDENCE_POLICY's rows for the milestone's category (E-U2).</summary>
internal sealed class MilestoneEvidencePolicy(IConfigurationResolver resolver)
{
    /// <summary>
    /// The evidence types mandatory for <paramref name="milestoneCategoryItemId"/> in the EVIDENCE_POLICY version in force at
    /// <paramref name="asOf"/>, with that version's id. While no version is in force — none published yet, or the last one
    /// withdrawn with none after it — nothing is mandatory: PTBC-006 is open, and its gate is that the backend enforces whatever
    /// policy is published. Versions that claim the same moment fail closed.
    /// </summary>
    /// <exception cref="ConfigurationMissingException">The policy cannot be resolved for a reason other than its absence.</exception>
    public async Task<EvidenceRequirement> RequirementAsync(Guid milestoneCategoryItemId, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ResolvedConfiguration policy;
        try
        {
            policy = await resolver.ResolveAsync(ConfigurationFamilyCodes.EvidencePolicy, asOf, cancellationToken).ConfigureAwait(false);
        }
        catch (ConfigurationMissingException missing) when (missing.Reason == ConfigurationMissingReason.NoEffectiveVersion)
        {
            return EvidenceRequirement.PendingPolicy;
        }

        return new EvidenceRequirement(
            policy.VersionId,
            [.. policy.Content.EvidenceRequirements.Where(r => r.MilestoneCategoryItemId == milestoneCategoryItemId && r.IsMandatory).Select(r => r.EvidenceTypeItemId).Distinct()]);
    }
}

/// <summary>What one policy version makes mandatory for one category; <see cref="PolicyVersionId"/> null while no version is published.</summary>
internal sealed record EvidenceRequirement(Guid? PolicyVersionId, IReadOnlyList<Guid> Mandatory)
{
    public static EvidenceRequirement PendingPolicy { get; } = new(null, []);

    /// <summary>The mandatory types the satisfied set lacks, in the policy's order.</summary>
    public IReadOnlyList<Guid> MissingFrom(IReadOnlySet<Guid> satisfied)
    {
        ArgumentNullException.ThrowIfNull(satisfied);
        return [.. Mandatory.Where(type => !satisfied.Contains(type))];
    }
}
