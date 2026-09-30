namespace PMPlatform.Domain.DocumentManagement;

/// <summary>ERD <c>evidence_reference.status</c>. A WITHDRAWN reference satisfies no requirement; it stays as history.</summary>
public enum EvidenceReferenceStatus
{
    Valid = 1,
    Withdrawn = 2,
}
