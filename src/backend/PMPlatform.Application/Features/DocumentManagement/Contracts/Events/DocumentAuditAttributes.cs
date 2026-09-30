namespace PMPlatform.Application.Features.DocumentManagement.Contracts.Events;

/// <summary>The attribute names of DocumentManagement's audit events.</summary>
public static class DocumentAuditAttributes
{
    public const string DocumentId = "document_id";
    public const string DocumentVersionId = "document_version_id";
    public const string VersionNo = "version_no";
    public const string FileName = "file_name";
    public const string ContentType = "content_type";
    public const string SizeBytes = "size_bytes";
    public const string ChecksumSha256 = "checksum_sha256";
    public const string ScanState = "scan_state";
    public const string ScanReference = "scan_reference";
    public const string Status = "status";
    public const string TitleText = "title";
    public const string DescriptionText = "description";
    public const string DocumentTypeItemId = "document_type_item_id";
    public const string DataClassificationItemId = "data_classification_item_id";
    public const string BusinessLinkId = "business_link_id";
    public const string LinkRole = "link_role";
    public const string TargetModule = "target_module";
    public const string TargetType = "target_type";
    public const string TargetId = "target_id";
    public const string EvidenceTypeItemId = "evidence_type_item_id";
}
