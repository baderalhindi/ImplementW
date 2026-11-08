namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>What keeps a department from being deleted (client review, item 6).</summary>
public sealed record DepartmentUses(int Users, int SubDepartments, int OpenProjects, int ActiveAssignments)
{
    public bool Any => Users + SubDepartments + OpenProjects + ActiveAssignments > 0;
}
