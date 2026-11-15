using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// An image the Home welcome banner can show (review round 8, point 07), kept in the database with its words: the place it shows in both languages, the
/// text that describes it, where its subject is so a crop keeps it, and who took it. The content is a WebP of at most 300 KB, prepared before upload.
/// </summary>
public sealed class BannerImage : AuditedEntity
{
    /// <summary>A stable name for the image, so uploading it again replaces it rather than adding a copy.</summary>
    public required string Code { get; set; }

    public required string CaptionAr { get; set; }

    public required string CaptionEn { get; set; }

    public required string AltAr { get; set; }

    public required string AltEn { get; set; }

    /// <summary>Where the subject is, in percent from the left; a crop keeps it in view.</summary>
    public short FocalX { get; set; }

    /// <summary>Where the subject is, in percent from the top.</summary>
    public short FocalY { get; set; }

    public string? Credit { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Content { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int SortOrder { get; set; }
}
