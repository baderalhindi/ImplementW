using Microsoft.Extensions.Logging.Abstractions;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>SR-10, AUTH-T13: a refresh token continues its session once, a replaced one ends it, and a signed-out session is not continued.</summary>
public sealed class SessionRegistryTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-00000000a201");
    private static readonly DateTimeOffset ExpiresAt = new(2026, 11, 25, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeUserSessionRepository _repository = new();
    private readonly SessionRegistry _registry;

    public SessionRegistryTests()
    {
        _registry = new SessionRegistry(_repository, NullLogger<SessionRegistry>.Instance);
    }

    [Fact]
    public async Task ASignInRecordsAnActiveSessionNamingItsRefreshToken()
    {
        SessionTokens signIn = Tokens(Guid.NewGuid());

        Assert.Null(await _registry.RecordAsync(UserId, signIn, continuation: null, CancellationToken.None));

        UserSession session = _repository.Sessions[signIn.SessionId];
        Assert.Equal((UserId, UserSessionStatus.Active, signIn.RefreshTokenId, ExpiresAt), (session.UserId, session.Status, session.RefreshTokenId, session.ExpiresAt));
    }

    [Fact]
    public async Task ARefreshReplacesTheRefreshTokenSoTheNextOneContinues()
    {
        SessionTokens signIn = await SignInAsync();
        SessionTokens next = Tokens(signIn.SessionId);

        Assert.Null(await _registry.CheckAsync(Continuation(signIn), CancellationToken.None));
        Assert.Null(await _registry.RecordAsync(UserId, next, Continuation(signIn), CancellationToken.None));

        Assert.Equal(next.RefreshTokenId, _repository.Sessions[signIn.SessionId].RefreshTokenId);
        Assert.Null(await _registry.CheckAsync(Continuation(next), CancellationToken.None));
    }

    [Fact]
    public async Task AReplacedRefreshTokenPresentedAgainRevokesTheSession()
    {
        SessionTokens signIn = await SignInAsync();
        SessionTokens next = Tokens(signIn.SessionId);
        await _registry.RecordAsync(UserId, next, Continuation(signIn), CancellationToken.None);

        Assert.Equal(AuthenticationFailureReason.RefreshTokenReused, await _registry.CheckAsync(Continuation(signIn), CancellationToken.None));

        UserSession session = _repository.Sessions[signIn.SessionId];
        Assert.Equal((UserSessionStatus.Revoked, UserSessionEndReason.RefreshTokenReused), (session.Status, session.EndReason));
        Assert.Equal(AuthenticationFailureReason.SessionEnded, await _registry.CheckAsync(Continuation(next), CancellationToken.None));
    }

    /// <summary>Two requests that both passed the check with the same token: the one that rotates second is a reuse.</summary>
    [Fact]
    public async Task OfTwoRefreshesWithTheSameTokenTheSecondRevokesTheSession()
    {
        SessionTokens signIn = await SignInAsync();

        Assert.Null(await _registry.RecordAsync(UserId, Tokens(signIn.SessionId), Continuation(signIn), CancellationToken.None));
        Assert.Equal(
            AuthenticationFailureReason.RefreshTokenReused,
            await _registry.RecordAsync(UserId, Tokens(signIn.SessionId), Continuation(signIn), CancellationToken.None));
        Assert.Equal(UserSessionStatus.Revoked, _repository.Sessions[signIn.SessionId].Status);
    }

    [Fact]
    public async Task ASignedOutSessionIsNotContinued()
    {
        SessionTokens signIn = await SignInAsync();

        bool ended = await _registry.SignOutAsync(UserId, signIn.SessionId, CancellationToken.None);
        bool endedAgain = await _registry.SignOutAsync(UserId, signIn.SessionId, CancellationToken.None);

        // DEV-C-01: only the call that ended the session says so; the sign-out audit rests on it.
        Assert.True(ended);
        Assert.False(endedAgain);
        UserSession session = _repository.Sessions[signIn.SessionId];
        Assert.Equal((UserSessionStatus.SignedOut, UserSessionEndReason.SignedOut), (session.Status, session.EndReason));
        Assert.Equal(AuthenticationFailureReason.SessionEnded, await _registry.CheckAsync(Continuation(signIn), CancellationToken.None));
        Assert.Equal(
            AuthenticationFailureReason.SessionEnded,
            await _registry.RecordAsync(UserId, Tokens(signIn.SessionId), Continuation(signIn), CancellationToken.None));
    }

    /// <summary>A refresh token issued before sessions were recorded, or for a session id never issued, continues nothing.</summary>
    [Fact]
    public async Task ASessionThatWasNeverRecordedIsNotContinued()
    {
        SessionTokens unknown = Tokens(Guid.NewGuid());

        Assert.Equal(AuthenticationFailureReason.SessionEnded, await _registry.CheckAsync(Continuation(unknown), CancellationToken.None));
    }

    [Fact]
    public async Task AnotherUsersTokenDoesNotContinueTheSession()
    {
        SessionTokens signIn = await SignInAsync();

        Assert.Equal(
            AuthenticationFailureReason.SessionEnded,
            await _registry.CheckAsync(Continuation(signIn) with { UserId = Guid.NewGuid() }, CancellationToken.None));
        Assert.Equal(UserSessionStatus.Active, _repository.Sessions[signIn.SessionId].Status);
    }

    private async Task<SessionTokens> SignInAsync()
    {
        SessionTokens signIn = Tokens(Guid.NewGuid());
        await _registry.RecordAsync(UserId, signIn, continuation: null, CancellationToken.None);
        return signIn;
    }

    private static SessionTokens Tokens(Guid sessionId) =>
        new("access", ExpiresAt, "refresh", ExpiresAt, ExpiresAt, sessionId, Guid.NewGuid());

    private static SessionContinuation Continuation(SessionTokens tokens) =>
        new(UserId, tokens.SessionId, new SessionAuthentication(AuthenticationMethod.Directory, MultiFactor: false, ExpiresAt.AddHours(-8)), ExpiresAt, tokens.RefreshTokenId);
}
