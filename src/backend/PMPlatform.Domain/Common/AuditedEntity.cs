namespace PMPlatform.Domain.Common;

/// <summary>
/// The columns every table carries (ERD D-1, D-2): an application-generated <c>id uuid</c> primary key and the four
/// audit columns. <see cref="CreatedBy"/> and <see cref="UpdatedBy"/> hold a <c>User.id</c> by convention, with no
/// foreign-key constraint; a service principal is a <c>User</c>, so neither is ever empty.
/// </summary>
public abstract class AuditedEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid UpdatedBy { get; set; }
}
