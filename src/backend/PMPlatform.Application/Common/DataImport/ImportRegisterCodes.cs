namespace PMPlatform.Application.Common.DataImport;

/// <summary>
/// Every register's code (TASK-102), in one place: a register names another's records by legacy ID through the lineage, by its code,
/// without referencing the module that owns it.
/// </summary>
public static class ImportRegisterCodes
{
    public const string Department = "DEPARTMENT";
    public const string MasterDataItem = "MASTER_DATA_ITEM";
    public const string ExternalEntity = "EXTERNAL_ENTITY";
    public const string User = "USER";
    public const string KpiDefinition = "KPI_DEFINITION";
    public const string Project = "PROJECT";
    public const string RoleAssignment = "ROLE_ASSIGNMENT";
    public const string ScheduleActivity = "SCHEDULE_ACTIVITY";
    public const string ProjectMilestone = "PROJECT_MILESTONE";
    public const string ProjectTask = "PROJECT_TASK";
    public const string Risk = "RISK";
    public const string ManagementConcern = "MANAGEMENT_CONCERN";
    public const string KpiMeasurement = "KPI_MEASUREMENT";
}
