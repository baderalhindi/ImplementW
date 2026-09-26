namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Configuration section <c>Identity:Directory</c>: the non-secret half of the directory integration. The defaults are
/// Active Directory's attribute names; the exact directory product is confirmed with AHDA IT at environment setup
/// (ADR-007 Impact). The connection itself — <c>AD_LDAP_URL</c>, <c>AD_BIND_DN</c>, <c>AD_BIND_PASSWORD</c> — comes from
/// the secret store (<see cref="DirectoryConnection"/>).
/// </summary>
internal sealed class DirectoryOptions
{
    public const string Section = "Identity:Directory";

    /// <summary>The attribute a person signs in with.</summary>
    public string UsernameAttribute { get; set; } = "sAMAccountName";

    /// <summary>
    /// The immutable identifier stored as <c>User.DirectorySubjectId</c>. <c>objectGUID</c> is binary and is stored in
    /// its canonical GUID form. The SSO subject claim must carry the same value (<see cref="SingleSignOnOptions.SubjectClaim"/>).
    /// </summary>
    public string SubjectAttribute { get; set; } = "objectGUID";

    public string JobTitleAttribute { get; set; } = "title";

    /// <summary>Its value is matched against <c>Department.DirectoryReference</c>.</summary>
    public string DepartmentAttribute { get; set; } = "department";

    /// <summary>A distinguished name; the manager's subject is read from it.</summary>
    public string ManagerAttribute { get; set; } = "manager";

    /// <summary>
    /// Plain <c>ldap://</c> exposes every password on the wire, so only <c>ldaps://</c> is used unless this is set. It is
    /// set only for the local test directory (infra/docker).
    /// </summary>
    public bool AllowUnencryptedConnection { get; set; }

    /// <summary>Bounds each connection attempt and each operation, so an unreachable directory fails fast.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsBinarySubject => string.Equals(SubjectAttribute, "objectGUID", StringComparison.OrdinalIgnoreCase);
}
