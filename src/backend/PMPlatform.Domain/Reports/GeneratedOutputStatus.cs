namespace PMPlatform.Domain.Reports;

/// <summary>A generated output's lifecycle (ERD §6): AVAILABLE until it expires; PURGED once its file is deleted, the record kept.</summary>
public enum GeneratedOutputStatus
{
    Available = 1,
    Expired = 2,
    Purged = 3,
}
