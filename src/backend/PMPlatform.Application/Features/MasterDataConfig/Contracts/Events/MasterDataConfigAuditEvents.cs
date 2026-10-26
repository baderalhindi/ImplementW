namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Events;

/// <summary>
/// The audit event types MasterDataConfig produces (TASK-034; event-conventions EV-1). All are CONFIGURATION_CHANGE:
/// each changes what the platform resolves or offers. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class MasterDataConfigAuditEvents
{
    public const string CatalogueRenamed = "MasterDataConfig.CatalogueRenamed";
    public const string ItemCreated = "MasterDataConfig.ItemCreated";
    public const string ItemUpdated = "MasterDataConfig.ItemUpdated";
    public const string ItemValidated = "MasterDataConfig.ItemValidated";
    public const string ItemPublished = "MasterDataConfig.ItemPublished";
    public const string ItemRetired = "MasterDataConfig.ItemRetired";
    public const string KpiDefinitionCreated = "MasterDataConfig.KpiDefinitionCreated";
    public const string KpiDefinitionUpdated = "MasterDataConfig.KpiDefinitionUpdated";
    public const string KpiDefinitionValidated = "MasterDataConfig.KpiDefinitionValidated";
    public const string KpiDefinitionPublished = "MasterDataConfig.KpiDefinitionPublished";
    public const string KpiDefinitionRetired = "MasterDataConfig.KpiDefinitionRetired";
    public const string VersionCreated = "MasterDataConfig.VersionCreated";
    public const string VersionUpdated = "MasterDataConfig.VersionUpdated";
    public const string VersionValidated = "MasterDataConfig.VersionValidated";
    public const string VersionPublished = "MasterDataConfig.VersionPublished";
    public const string VersionRetired = "MasterDataConfig.VersionRetired";
}
