using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// An organisation delivering a regional project: a government entity, a public authority or a private company
/// (ADR-013, ADM-013). Delete policy: RETAIN.
/// </summary>
public sealed class ExternalEntity : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    /// <summary>Master data item of catalogue EXTERNAL_ENTITY_TYPE.</summary>
    public Guid EntityTypeItemId { get; set; }

    public ExternalEntityStatus Status { get; set; }

    /// <summary>The named AHDA sponsor of the entity relationship (TASK-031).</summary>
    public Guid? SponsorUserId { get; set; }
}
