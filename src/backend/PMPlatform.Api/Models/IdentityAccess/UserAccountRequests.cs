namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>A temporary password the administrator typed; left out, the platform generates one.</summary>
public sealed record UserPasswordResetRequest(string? Password = null);

/// <summary>The account's last usable day; null makes it open-ended.</summary>
public sealed record UserEndDateRequest(DateOnly? EndsOn = null);
