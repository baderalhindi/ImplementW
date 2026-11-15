using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>How the Home welcome banner looks for one person (review round 8, point 07).</summary>
public enum BannerStyle
{
    /// <summary>The authority's green banner, as it always was. Everyone starts on it.</summary>
    Default = 1,

    /// <summary>One image the person picked.</summary>
    Image = 2,

    /// <summary>The images the person picked, one after another.</summary>
    Rotate = 3,
}

/// <summary>
/// One person's banner choice, kept on the server so it follows them across browsers (review round 8, point 07). The images stay chosen while the
/// style is the green one, so switching back to images brings them back. A person with no row sees the green banner.
/// </summary>
public sealed class UserBannerPreference : AuditedEntity
{
    public Guid UserId { get; set; }

    public BannerStyle Style { get; set; }

    /// <summary>The images chosen, in the order they show.</summary>
    public List<Guid> ImageIds { get; set; } = [];
}
