namespace PMPlatform.Domain.Common;

/// <summary>
/// A record a person may delete while it stays in the database (client review, items 4 and 8): set, it leaves every read through the
/// platform's query filter, together with the records that belong to it. The audit trail keeps the deletion.
/// </summary>
public interface IDeletable
{
    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }
}
