namespace PMPlatform.Domain.DocumentManagement;

/// <summary>
/// ERD <c>document_version.scan_state</c> (CTL-20). Only CLEAN content is ever served or referenced as evidence. CLEAN
/// and QUARANTINED are final; a SCAN_FAILED version may be queued for scanning again.
/// </summary>
public enum ScanState
{
    ScanPending = 1,
    Clean = 2,
    Quarantined = 3,
    ScanFailed = 4,
}
