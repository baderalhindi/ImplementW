using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Application.Features.Approval;

/// <summary>A task a run starts with: a stage and the role that decides it.</summary>
internal sealed record RoutedSeat(short SequenceNo, Guid ApproverRoleId);

/// <summary>
/// Routing (Blueprint Section 11) over the APPROVAL_AUTHORITY matrix. A row applies when its subject type is the routing
/// key and each of its conditions — governance profile, band, minimum amount — is absent or met; every applicable row
/// is a task. Stages run in sequence order; every task of a stage must be approved before the next begins.
/// </summary>
/// <remarks>
/// Fails closed (Blueprint Section 12): no applicable row, or two applicable rows naming the same role for the same
/// stage (ERD O-4: the matrix has no business key), is missing configuration, never a guessed route. A row's
/// <c>is_mandatory</c> is not given a weaker meaning: every routed row must approve (record F-6).
/// </remarks>
internal static class ApprovalRouting
{
    public static IReadOnlyList<RoutedSeat> Route(ResolvedConfiguration authority, ApprovalStart start)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(start);

        List<ApprovalAuthorityEntry> applicable = [.. authority.Content.ApprovalAuthority.Where(rule => Applies(rule, start))];
        return applicable.Count == 0
            ? throw new ConfigurationMissingException(
                ConfigurationFamilyCodes.ApprovalAuthority, ConfigurationMissingReason.EntryMissing, $"approval authority for {start.RoutingKey}")
            : applicable.GroupBy(rule => (rule.SequenceNo, rule.ApproverRoleId)).Any(seat => seat.Count() > 1)
                ? throw new ConfigurationMissingException(
                    ConfigurationFamilyCodes.ApprovalAuthority, ConfigurationMissingReason.EntryInvalid, $"approval authority for {start.RoutingKey}")
                : [.. applicable.OrderBy(rule => rule.SequenceNo).ThenBy(rule => rule.ApproverRoleId).Select(rule => new RoutedSeat(rule.SequenceNo, rule.ApproverRoleId))];
    }

    private static bool Applies(ApprovalAuthorityEntry rule, ApprovalStart start) =>
        string.Equals(rule.SubjectTypeCode, start.RoutingKey, StringComparison.Ordinal)
        && (rule.GovernanceProfileItemId is null || rule.GovernanceProfileItemId == start.GovernanceProfileItemId)
        && (rule.BandNo is null || rule.BandNo == start.BandNo)
        && (rule.MinAmountSar is null || (start.AmountSar is { } amount && amount.Amount >= rule.MinAmountSar));
}
