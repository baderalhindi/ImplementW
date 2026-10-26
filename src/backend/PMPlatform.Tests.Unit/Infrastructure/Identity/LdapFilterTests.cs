using PMPlatform.Infrastructure.Identity;

namespace PMPlatform.Tests.Unit.Infrastructure.Identity;

/// <summary>
/// TASK-077: the sign-in username is written into a directory search filter, an injection context of its own. RFC 4515 escaping keeps
/// any username a single assertion value: it can neither widen the search nor add a clause (output-encoding-checklist.md OE-12).
/// </summary>
public sealed class LdapFilterTests
{
    [Theory]
    [InlineData("local.r02", "(uid=local.r02)")]
    [InlineData("*", @"(uid=\2a)")]
    [InlineData("admin)(|(uid=*", @"(uid=admin\29\28|\28uid=\2a)")]
    [InlineData(@"back\slash", @"(uid=back\5cslash)")]
    [InlineData("nul\u0000", @"(uid=nul\00)")]
    [InlineData("سارة", @"(uid=\d8\b3\d8\a7\d8\b1\d8\a9)")]
    public void AUsernameIsOneEscapedAssertionValue(string username, string filter) => Assert.Equal(filter, LdapFilter.Equality("uid", username));
}
