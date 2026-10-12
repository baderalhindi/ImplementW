namespace PMPlatform.Domain.AuditActivity;

/// <summary>
/// A row of committed audit evidence (FG-06 BR-AUD-003, AUD-CC-06; TASK-073). No application path updates or deletes it:
/// the persistence layer refuses the change before it reaches the database, and the database refuses it for every role.
/// </summary>
public interface IAuditEvidence;
