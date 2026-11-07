using System.Text;
using PMPlatform.Infrastructure.Identity;

namespace PMPlatform.Tests.Unit.Infrastructure.Identity;

public sealed class DirectoryPasswordEncodingTests
{
    [Fact]
    public void ActiveDirectoryValueIsTheQuotedPasswordInUtf16LittleEndian() =>
        Assert.Equal(new byte[] { 0x22, 0x00, 0x61, 0x00, 0x31, 0x00, 0x22, 0x00 }, DirectoryPasswordEncoding.ActiveDirectoryValue("a1"));

    [Fact]
    public void PasswordModifyRequestNamesNoUserAndCarriesBothPasswords()
    {
        byte[] expected = [0x30, 0x0A, 0x81, 0x03, .. Encoding.ASCII.GetBytes("old"), 0x82, 0x03, .. Encoding.ASCII.GetBytes("new")];

        Assert.Equal(expected, DirectoryPasswordEncoding.PasswordModifyRequest("old", "new"));
    }
}
