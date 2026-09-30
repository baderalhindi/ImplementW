using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A holder and how to address them. The e-mail address is the directory's; the mobile number is <see cref="IUserContactDirectory"/>'s.</summary>
public sealed record RoleHolder(Guid UserId, string Email, Language PreferredLanguage);
