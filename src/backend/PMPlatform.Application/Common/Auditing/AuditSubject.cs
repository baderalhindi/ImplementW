namespace PMPlatform.Application.Common.Auditing;

/// <summary>The record the event is about, as an opaque reference (ERD D-14: Audit is subject-agnostic).</summary>
public sealed record AuditSubject(string Module, string Type, Guid Id);
