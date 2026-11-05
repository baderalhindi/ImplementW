namespace PMPlatform.Domain.IdentityAccess;

/// <summary>Who holds the account's credentials (the 5 Nov 2026 review, R4-6): AHDA's directory, or the platform for a local account.</summary>
public enum AccountSource
{
    Directory = 1,
    Local = 2,
}
