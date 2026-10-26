namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The directory settings, secrets reduced to whether they are set. The attribute names are the directory-to-platform
/// mapping ADR-007 allows: the subject that identifies the person, and the three attributes the directory is
/// authoritative for. There is no group-to-role mapping: platform roles are assigned in the platform.
/// </summary>
public sealed record DirectoryIntegrationStatus(
    bool IsConfigured,
    bool UrlConfigured,
    bool BindDnConfigured,
    bool BindPasswordConfigured,
    bool UsesTransportSecurity,
    string UsernameAttribute,
    string SubjectAttribute,
    string JobTitleAttribute,
    string DepartmentAttribute,
    string ManagerAttribute);
