using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.IdentityAccess.AdministrationFixture;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>ADR-004: a mobile number is verified only by its holder, and only an active user's verified number is ever handed out.</summary>
public sealed class MobileNumberVerificationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-00000000a101");

    private readonly AdministrationFixture _fixture = new();
    private readonly User _user = Internal(UserId);

    public MobileNumberVerificationServiceTests()
    {
        _user.MobileNumber = "+966500000001";
        _fixture.Users.Put(_user);
    }

    [Fact]
    public async Task TheCodeSentToTheNumberVerifiesIt()
    {
        AdministrationResult<MobileVerificationChallenge> challenge = await _fixture.MobileService().StartAsync(UserId, CancellationToken.None);
        AdministrationResult<MobileVerificationResult> result = await _fixture.MobileService().CompleteAsync(
            UserId, challenge.Value!.ChallengeId, FakeMobileNumberVerifier.Code, CancellationToken.None);

        Assert.Equal((UserId, "+966500000001"), _fixture.Verifier.SentTo);
        Assert.Equal(new MobileVerificationResult("+966500000001", Now), result.Value);
        Assert.Equal((Now, UserId), (_user.MobileVerifiedAt, _user.UpdatedBy));
    }

    [Fact]
    public async Task AWrongCodeVerifiesNothing()
    {
        await _fixture.MobileService().StartAsync(UserId, CancellationToken.None);

        AdministrationResult<MobileVerificationResult> result = await _fixture.MobileService().CompleteAsync(
            UserId, FakeMobileNumberVerifier.ChallengeId, "000000", CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.MobileVerificationFailed, result.Error!.Code);
        Assert.Null(_user.MobileVerifiedAt);
        Assert.Equal(0, _fixture.Users.Saves);
    }

    /// <summary>The number is changed after the code was sent: the code proved the old number, so it verifies nothing.</summary>
    [Fact]
    public async Task ACodeSentToAnEarlierNumberDoesNotVerifyTheCurrentOne()
    {
        await _fixture.MobileService().StartAsync(UserId, CancellationToken.None);
        _user.MobileNumber = "+966500000002";

        AdministrationResult<MobileVerificationResult> result = await _fixture.MobileService().CompleteAsync(
            UserId, FakeMobileNumberVerifier.ChallengeId, FakeMobileNumberVerifier.Code, CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.MobileVerificationFailed, result.Error!.Code);
        Assert.Null(_user.MobileVerifiedAt);
    }

    [Fact]
    public async Task WithoutAConfiguredProviderVerificationIsUnavailable()
    {
        _fixture.Verifier.IsConfigured = false;

        AdministrationResult<MobileVerificationChallenge> start = await _fixture.MobileService().StartAsync(UserId, CancellationToken.None);
        AdministrationResult<MobileVerificationResult> complete = await _fixture.MobileService().CompleteAsync(UserId, "any", "any", CancellationToken.None);

        Assert.Equal(AdministrationErrorKind.Unavailable, start.Error!.Kind);
        Assert.Equal(AdministrationErrorKind.Unavailable, complete.Error!.Kind);
        Assert.Null(_user.MobileVerifiedAt);
    }

    [Fact]
    public async Task ThereMustBeAnUnverifiedNumberOfAnActiveUser()
    {
        _user.MobileNumber = null;
        AdministrationResult<MobileVerificationChallenge> noNumber = await _fixture.MobileService().StartAsync(UserId, CancellationToken.None);
        _user.MobileNumber = "+966500000001";
        _user.MobileVerifiedAt = Now;
        AdministrationResult<MobileVerificationChallenge> verified = await _fixture.MobileService().StartAsync(UserId, CancellationToken.None);
        _user.MobileVerifiedAt = null;
        _user.Status = UserStatus.Disabled;
        AdministrationResult<MobileVerificationChallenge> disabled = await _fixture.MobileService().StartAsync(UserId, CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.MobileNumberMissing, noNumber.Error!.Code);
        Assert.Equal(AdministrationErrorKind.InvalidTransition, verified.Error!.Kind);
        Assert.Equal(AdministrationErrorKind.Forbidden, disabled.Error!.Kind);
        Assert.Null(_fixture.Verifier.SentTo);
    }

    [Fact]
    public async Task OnlyAVerifiedNumberOfAnActiveUserIsHandedOut()
    {
        UserContactDirectory directory = new(_fixture.Users);

        string? unverified = await directory.FindVerifiedMobileNumberAsync(UserId, CancellationToken.None);
        _user.MobileVerifiedAt = Now;
        string? verified = await directory.FindVerifiedMobileNumberAsync(UserId, CancellationToken.None);
        _user.Status = UserStatus.Disabled;
        string? disabled = await directory.FindVerifiedMobileNumberAsync(UserId, CancellationToken.None);

        Assert.Null(unverified);
        Assert.Equal("+966500000001", verified);
        Assert.Null(disabled);
    }
}
