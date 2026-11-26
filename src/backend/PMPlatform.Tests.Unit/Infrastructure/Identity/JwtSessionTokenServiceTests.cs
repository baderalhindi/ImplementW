using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Infrastructure.Identity;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Tests.Unit.Infrastructure.Identity;

/// <summary>SR-10: every refresh token carries its own id, which the session record must name for the token to continue the session.</summary>
public sealed class JwtSessionTokenServiceTests
{
    private readonly JwtSessionTokenService _tokens = new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ApplicationSecrets.JwtSigningKey] = new string('k', 32) }).Build(),
        Options.Create(new SessionTokenOptions()),
        TimeProvider.System);

    private static readonly SessionTokenSubject Subject = new(
        Guid.NewGuid(), UserType.Internal, new SessionAuthentication(AuthenticationMethod.Directory, MultiFactor: false, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds())), ["R02"]);

    [Fact]
    public async Task EachTokenPairOfASessionHasANewRefreshTokenId()
    {
        SessionTokens signIn = _tokens.Issue(Subject, continuation: null);
        SessionContinuation read = (await _tokens.ReadRefreshTokenAsync(signIn.RefreshToken))!;
        SessionTokens next = _tokens.Issue(Subject, read);

        Assert.Equal((signIn.SessionId, signIn.RefreshTokenId), (read.SessionId, read.RefreshTokenId));
        Assert.Equal(signIn.SessionId, next.SessionId);
        Assert.NotEqual(signIn.RefreshTokenId, next.RefreshTokenId);
        Assert.NotEqual(signIn.RefreshToken, next.RefreshToken);
        Assert.Equal(next.RefreshTokenId, (await _tokens.ReadRefreshTokenAsync(next.RefreshToken))!.RefreshTokenId);
    }
}
