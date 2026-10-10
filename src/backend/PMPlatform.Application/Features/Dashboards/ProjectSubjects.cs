using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>A project as the authorization engine decides on it.</summary>
internal static class ProjectSubjects
{
    /// <summary>The project's anchors (M-7), with the classification of what is read of it (ADR-010).</summary>
    public static AuthorizationSubject Of(ProjectFacts project, Guid? classificationId)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new()
        {
            ProjectId = project.Id,
            DepartmentId = project.DepartmentId,
            ExternalEntityId = project.ExternalEntityId,
            OwnerUserId = project.ProjectManagerUserId,
            DataClassificationItemId = classificationId,
        };
    }
}
