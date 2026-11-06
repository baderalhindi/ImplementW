using PMPlatform.Application.Features.IdentityAccess.Authentication;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>The password rules of a local account (the 5 Nov 2026 review §6.1).</summary>
public sealed class LocalPasswordTests
{
    [Theory]
    [InlineData("short1A", "someone", false)]
    [InlineData("onlylettersonly", "someone", false)]
    [InlineData("123456789012", "someone", false)]
    [InlineData("Twelve4Chars", "someone", true)]
    [InlineData("Local4Account", "local4account", false)]
    public void ANewPasswordHasTwelveCharactersLettersAndDigitsAndIsNotTheUsername(string password, string username, bool expected) =>
        Assert.Equal(expected, LocalPassword.MeetsRules(password, username));

    [Fact]
    public void AHashVerifiesItsOwnPasswordOnlyAndNeverHoldsThePlainText()
    {
        const string password = "Twelve4Chars";
        string hash = LocalPassword.Hash(password);
        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
        Assert.True(LocalPassword.Verify(password, hash));
        Assert.False(LocalPassword.Verify("Twelve4Charz", hash));
        Assert.NotEqual(hash, LocalPassword.Hash(password));
    }

    [Fact]
    public void AGeneratedPasswordMeetsTheRules() =>
        Assert.True(LocalPassword.MeetsRules(LocalPassword.Generate(), "someone"));
}
