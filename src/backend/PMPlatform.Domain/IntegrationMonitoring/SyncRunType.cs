namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>What a sync run does (ERD <c>sync_run.run_type</c>).</summary>
public enum SyncRunType
{
    /// <summary>Copies source data into the platform. None is built: a directory sync would change FG-03's data, which only IdentityAccess may do.</summary>
    DirectorySync = 1,

    /// <summary>Compares the source with the platform's copy and reports each mismatch; changes neither (BR-INT-006).</summary>
    Reconciliation = 2,
}
