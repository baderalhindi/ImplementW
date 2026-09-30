using PMPlatform.Infrastructure.Notifications;

namespace PMPlatform.Tests.Unit.Infrastructure.Notifications;

/// <summary>What stops the API at start-up when <c>EXCHANGE_SMTP_HOST</c> is set: the relay settings must be usable together.</summary>
public sealed class SmtpOptionsTests
{
    [Fact]
    public void WithoutAHostNothingIsCheckedAndTheChannelIsOff()
    {
        SmtpOptions options = new() { PortText = "not a port" };
        Assert.False(options.IsConfigured);
        Assert.True(options.IsValid());
    }

    [Fact]
    public void AHostWithASenderAndNoCredentialsIsAnOpenRelayOnPort587()
    {
        SmtpOptions options = Relay();
        Assert.True(options.IsValid());
        Assert.Equal(587, options.Port);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("25x")]
    [InlineData("-25")]
    public void APortOutsideTheRangeIsRefused(string port)
    {
        SmtpOptions options = Relay();
        options.PortText = port;
        Assert.False(options.IsValid());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    public void AHostWithoutAUsableSenderIsRefused(string? from)
    {
        SmtpOptions options = Relay();
        options.FromAddress = from;
        Assert.False(options.IsValid());
    }

    [Theory]
    [InlineData("svc-pmplatform", null)]
    [InlineData(null, "secret")]
    public void AUserWithoutAPasswordOrAPasswordWithoutAUserIsRefused(string? user, string? password)
    {
        SmtpOptions options = Relay();
        options.User = user;
        options.Password = password;
        Assert.False(options.IsValid());
    }

    private static SmtpOptions Relay() => new() { Host = "smtp.ahda.test", FromAddress = "pmplatform-noreply@ahda.test" };
}
