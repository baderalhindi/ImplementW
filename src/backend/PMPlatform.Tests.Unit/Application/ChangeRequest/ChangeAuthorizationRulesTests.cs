using PMPlatform.Application.Features.ChangeRequest;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Tests.Unit.Application.ChangeRequest;

/// <summary>
/// When an authorisation applies (WF-08 VAL-CHG-015, VAL-CHG-016, BR-CHG-021): to its project, its kind of change and the target version
/// it pins, while its change is being implemented, once — and the same application again is recognised as such.
/// </summary>
public sealed class ChangeAuthorizationRulesTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid Baseline = Guid.NewGuid();
    private const string Reference = "Schedule.ProjectBaseline:candidate";

    [Fact]
    public void AnIssuedAuthorizationOfAChangeBeingImplementedAppliesToItsPinnedTarget() =>
        Assert.Equal(ChangeAuthorizationVerdict.Applicable, Judge(Authorization(), Request(), Claim()));

    [Fact]
    public void ItAppliesToItsOwnProjectAndKindOfChangeOnly()
    {
        Assert.Equal(ChangeAuthorizationVerdict.OutOfScope, Judge(Authorization(), Request(), Claim() with { ProjectId = Guid.NewGuid() }));
        Assert.Equal(ChangeAuthorizationVerdict.OutOfScope, Judge(Authorization(), Request(), Claim() with { Scope = ChangeAuthorizationScope.CommitmentChange }));
    }

    /// <summary>Version-pinned: a target that moved since approval — another baseline, or the same one at another version — is refused.</summary>
    [Fact]
    public void ATargetThatMovedIsRefused()
    {
        Assert.Equal(ChangeAuthorizationVerdict.TargetMoved, Judge(Authorization(), Request(), Claim() with { TargetId = Guid.NewGuid() }));
        Assert.Equal(ChangeAuthorizationVerdict.TargetMoved, Judge(Authorization(), Request(), Claim() with { TargetRevisionNo = 2 }));
    }

    /// <summary>Approval is not application: only a change AHDA is implementing applies its authorisations.</summary>
    [Theory]
    [InlineData(ChangeRequestStatus.Approved)]
    [InlineData(ChangeRequestStatus.Implemented)]
    [InlineData(ChangeRequestStatus.Closed)]
    public void OnlyAChangeBeingImplementedApplies(ChangeRequestStatus status) =>
        Assert.Equal(ChangeAuthorizationVerdict.NotImplementing, Judge(Authorization(), Request(status), Claim()));

    /// <summary>Exactly once: the same application again is recognised; any other use, and a check, find it consumed.</summary>
    [Fact]
    public void AnAppliedAuthorizationIsReplayedToItsOwnApplicationAndConsumedForAnyOther()
    {
        ChangeAuthorization applied = Authorization(ChangeAuthorizationStatus.Applied, Reference);
        Assert.Equal(ChangeAuthorizationVerdict.Replayed, ChangeAuthorizationRules.Judge(applied, Request(), Claim(), Reference, Now));
        Assert.Equal(ChangeAuthorizationVerdict.Consumed, ChangeAuthorizationRules.Judge(applied, Request(), Claim(), "Schedule.ProjectBaseline:other", Now));
        Assert.Equal(ChangeAuthorizationVerdict.Consumed, ChangeAuthorizationRules.Judge(applied, Request(), Claim(), null, Now));
    }

    [Fact]
    public void AnExpiredOrRevokedAuthorizationHasEnded()
    {
        Assert.Equal(ChangeAuthorizationVerdict.Ended, Judge(Authorization(ChangeAuthorizationStatus.Revoked), Request(), Claim()));
        Assert.Equal(ChangeAuthorizationVerdict.Ended, Judge(Authorization(expiresAt: Now), Request(), Claim()));
        Assert.Equal(ChangeAuthorizationVerdict.Applicable, Judge(Authorization(expiresAt: Now.AddSeconds(1)), Request(), Claim()));
    }

    [Fact]
    public void AnUnknownAuthorizationIsNotFound() =>
        Assert.Equal(ChangeAuthorizationVerdict.NotFound, ChangeAuthorizationRules.Judge(null, null, Claim(), null, Now));

    private static ChangeAuthorizationVerdict Judge(ChangeAuthorization authorization, ChangeRequestEntity request, ChangeAuthorizationClaim claim) =>
        ChangeAuthorizationRules.Judge(authorization, request, claim, Reference, Now);

    private static ChangeAuthorizationClaim Claim() => new(Guid.NewGuid(), ProjectId, ChangeAuthorizationScope.Rebaseline, Baseline, 1);

    private static ChangeRequestEntity Request(ChangeRequestStatus status = ChangeRequestStatus.Implementation) => new()
    {
        ProjectId = ProjectId,
        Title = new NarrativeText("Change", Language.En),
        Justification = new NarrativeText("Why", Language.En),
        Status = status,
    };

    private static ChangeAuthorization Authorization(
        ChangeAuthorizationStatus status = ChangeAuthorizationStatus.Issued, string? appliedReference = null, DateTimeOffset? expiresAt = null) => new()
        {
            AuthorizationScope = ChangeAuthorizationScope.Rebaseline,
            TargetModule = "Schedule",
            TargetType = "ProjectBaseline",
            TargetId = Baseline,
            TargetRevisionNo = 1,
            IdempotencyKey = "key",
            Status = status,
            IssuedAt = Now.AddDays(-1),
            ExpiresAt = expiresAt,
            AppliedReference = appliedReference,
        };
}
