namespace PMPlatform.Application.Common.Events;

/// <summary>The aggregate the event is about, as an opaque reference (M-4, ERD D-15). <see cref="RevisionNo"/> is null for an unrevisioned aggregate.</summary>
public sealed record EventSubject(string Module, string Type, Guid Id, int? RevisionNo);
