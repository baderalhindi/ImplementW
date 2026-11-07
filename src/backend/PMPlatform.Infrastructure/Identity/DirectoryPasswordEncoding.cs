using System.Formats.Asn1;
using System.Text;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>How a directory expects a person's own password change to be written.</summary>
internal enum DirectoryPasswordProtocol
{
    /// <summary>Delete the old <c>unicodePwd</c> value and add the new one, in one modify. Active Directory requires an encrypted connection for it.</summary>
    ActiveDirectory = 0,

    /// <summary>The LDAP Password Modify extended operation (RFC 3062), which OpenLDAP hashes on arrival.</summary>
    PasswordModify = 1,
}

/// <summary>The values each <see cref="DirectoryPasswordProtocol"/> sends.</summary>
internal static class DirectoryPasswordEncoding
{
    public const string ActiveDirectoryAttribute = "unicodePwd";

    public const string PasswordModifyOid = "1.3.6.1.4.1.4203.1.11.1";

    /// <summary>The password in double quotes, in UTF-16LE, as Active Directory's <c>unicodePwd</c> requires.</summary>
    public static byte[] ActiveDirectoryValue(string password) => Encoding.Unicode.GetBytes($"\"{password}\"");

    /// <summary>RFC 3062 <c>PasswdModifyRequestValue</c> with no <c>userIdentity</c>: the change applies to whoever is bound.</summary>
    public static byte[] PasswordModifyRequest(string currentPassword, string newPassword)
    {
        AsnWriter writer = new(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteOctetString(Encoding.UTF8.GetBytes(currentPassword), new Asn1Tag(TagClass.ContextSpecific, 1));
            writer.WriteOctetString(Encoding.UTF8.GetBytes(newPassword), new Asn1Tag(TagClass.ContextSpecific, 2));
        }

        return writer.Encode();
    }
}
