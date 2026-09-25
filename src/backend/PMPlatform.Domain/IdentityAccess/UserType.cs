namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// EXTERNAL covers contributors and entity Project Managers alike: what makes an entity Project Manager is the
/// grant, not the person (ADR-013). SERVICE is the non-human principal that lets <c>created_by</c> be not null.
/// </summary>
public enum UserType
{
    Internal = 1,
    External = 2,
    Service = 3,
}
