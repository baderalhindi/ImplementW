namespace PMPlatform.Application.Common.Deletion;

/// <summary>
/// Rows that reference a record about to be deleted: <see cref="Record"/> is the referencing table (<c>project.project</c>, or <c>project.project.closed</c>
/// for closed projects, which take no change), <see cref="Field"/> its column, <see cref="Required"/> whether those rows need a value there.
/// </summary>
public sealed record RecordUse(string Record, string Field, bool Required, int Count);

/// <summary>What deleting a record did: deleted it after clearing it from <see cref="ClearedCount"/> rows, or nothing, because of <see cref="BlockedBy"/>.</summary>
public sealed record DeletionOutcome(bool Deleted, int ClearedCount, IReadOnlyList<RecordUse> BlockedBy);
