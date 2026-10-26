using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// <c>JWT_SIGNING_KEY</c>, read at each use rather than once, so a rotated key (TASK-019) takes effect on a running
/// instance: tokens signed with the previous key are rejected from the next read on.
/// </summary>
internal static class SessionSigningKey
{
    /// <summary>HS256 needs a key at least as long as its 256-bit hash.</summary>
    private const int MinimumLengthInBytes = 32;

    public static byte[] ReadBytes(IConfiguration configuration)
    {
        string? value = configuration[ApplicationSecrets.JwtSigningKey];
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"{ApplicationSecrets.JwtSigningKey} is not configured.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return bytes.Length >= MinimumLengthInBytes
            ? bytes
            : throw new InvalidOperationException($"{ApplicationSecrets.JwtSigningKey} is shorter than {MinimumLengthInBytes} bytes.");
    }

    public static SymmetricSecurityKey Read(IConfiguration configuration) => new(ReadBytes(configuration));
}
