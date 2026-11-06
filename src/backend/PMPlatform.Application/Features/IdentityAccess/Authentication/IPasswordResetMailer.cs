using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// Sends password reset links. Queuing returns at once: the time an SMTP exchange takes must not tell a caller that the account exists. Implemented in
/// Infrastructure/Identity.
/// </summary>
public interface IPasswordResetMailer
{
    /// <summary>False when the environment has no mail relay or no public address to link to, so no link could reach anyone.</summary>
    public bool IsAvailable { get; }

    /// <summary>Queues the message; a full queue drops it, and the person asks again.</summary>
    public void Enqueue(PasswordResetMail mail);
}
