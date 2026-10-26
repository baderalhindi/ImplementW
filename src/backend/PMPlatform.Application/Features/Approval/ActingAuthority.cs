namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// Whose authority a decision is made under: the actor's own (<see cref="DelegationId"/> null) or, through that
/// delegation, the delegator's. <see cref="HolderUserId"/> is the user whose grants were evaluated.
/// </summary>
internal sealed record ActingAuthority(Guid HolderUserId, Guid? DelegationId);

/// <summary>The authority found, or why there is none.</summary>
internal sealed record AuthorityCheck(ActingAuthority? Authority, ApprovalRefusal Refusal)
{
    public static AuthorityCheck Refused(ApprovalRefusal refusal) => new(null, refusal);
}
