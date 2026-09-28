namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>
/// A row of the approval authority matrix. A null profile applies it to every profile. ADR-013 (no external approval
/// authority) is enforced when a decision is made, on the deciding user (TASK-035).
/// </summary>
public sealed record ApprovalAuthorityEntry(
    string SubjectTypeCode,
    Guid? GovernanceProfileItemId,
    short? BandNo,
    decimal? MinAmountSar,
    short SequenceNo,
    Guid ApproverRoleId,
    bool IsMandatory);
