using PMPlatform.Application.Features.Project.Contracts;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

internal static class ProjectMapping
{
    public static ProjectDetail ToDetail(ProjectEntity p) => new(
        p.Id, p.FormalProjectId, p.Title, p.Description, p.ClassificationItemId, p.DepartmentId, p.ExternalEntityId, p.ProjectManagerUserId,
        p.LifecycleState, p.RevisionNo, p.GovernanceProfileItemId, p.ParticipationMode, p.RegistrationBudgetSar, p.PlannedStartDate, p.PlannedEndDate,
        p.RegionItemId, p.CityItemId, p.Latitude, p.Longitude, p.LegacyIntakeDate, p.ActivatedAt, p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy);

    public static ProjectSummary ToSummary(ProjectEntity p) => new(
        p.Id, p.FormalProjectId, p.Title, p.ClassificationItemId, p.DepartmentId, p.ExternalEntityId, p.ProjectManagerUserId, p.LifecycleState,
        p.ParticipationMode, p.LegacyIntakeDate, p.UpdatedAt);

    /// <summary>The registration fields as they stand, to compare an edit against.</summary>
    public static ProjectDraft ToDraft(ProjectEntity p) => new(
        p.Title, p.Description, p.ClassificationItemId, p.DepartmentId, p.ExternalEntityId, p.ParticipationMode, p.GovernanceProfileItemId,
        p.RegistrationBudgetSar, p.PlannedStartDate, p.PlannedEndDate, p.RegionItemId, p.CityItemId, p.Latitude, p.Longitude);

    /// <summary>Writes the registration fields of <paramref name="draft"/> onto the project.</summary>
    public static void Apply(ProjectDraft draft, ProjectEntity p)
    {
        p.Title = draft.Title;
        p.Description = draft.Description;
        p.ClassificationItemId = draft.ClassificationItemId;
        p.DepartmentId = draft.DepartmentId;
        p.ExternalEntityId = draft.ExternalEntityId;
        p.ParticipationMode = draft.ParticipationMode;
        p.GovernanceProfileItemId = draft.GovernanceProfileItemId;
        p.RegistrationBudgetSar = draft.RegistrationBudgetSar;
        p.PlannedStartDate = draft.PlannedStartDate;
        p.PlannedEndDate = draft.PlannedEndDate;
        p.RegionItemId = draft.RegionItemId;
        p.CityItemId = draft.CityItemId;
        p.Latitude = draft.Latitude;
        p.Longitude = draft.Longitude;
    }
}
