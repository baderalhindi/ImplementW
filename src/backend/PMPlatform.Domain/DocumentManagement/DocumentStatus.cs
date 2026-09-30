namespace PMPlatform.Domain.DocumentManagement;

/// <summary>ERD <c>document.status</c>. An ARCHIVED document takes no new version; its versions stay readable.</summary>
public enum DocumentStatus
{
    Active = 1,
    Archived = 2,
}
