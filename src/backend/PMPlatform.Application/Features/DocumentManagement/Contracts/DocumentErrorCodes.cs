namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>The DocumentManagement module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class DocumentErrorCodes
{
    /// <summary>
    /// 409: the version is not CLEAN — its scan is pending, failed, or found malware — so its content is not served and it
    /// cannot be evidence (api-conventions R-8; CTL-20).
    /// </summary>
    public const string NotAvailable = "DOCUMENT_NOT_AVAILABLE";

    /// <summary>422: a document type, classification, evidence type or project that is unknown or may not be used.</summary>
    public const string ReferenceInvalid = "DOCUMENT_REFERENCE_INVALID";

    /// <summary>409: another version of the document was added at the same moment; add the file again.</summary>
    public const string VersionConflict = "DOCUMENT_VERSION_CONFLICT";

    /// <summary>422: the version belongs to another document than the link's.</summary>
    public const string VersionMismatch = "DOCUMENT_VERSION_MISMATCH";

    /// <summary>
    /// 422: the link was ended by an unlink. It takes no evidence, and the same document, target and role cannot be linked
    /// again: an ended link stays as history (document-management.md F-7).
    /// </summary>
    public const string LinkEnded = "DOCUMENT_LINK_ENDED";

    /// <summary>422: this version was withdrawn as this evidence type on this link; the withdrawal stays as history (F-7).</summary>
    public const string EvidenceWithdrawn = "DOCUMENT_EVIDENCE_WITHDRAWN";
}
