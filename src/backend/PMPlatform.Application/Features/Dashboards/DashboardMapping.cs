using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>Between a version's rows and its representations. Role ids and codes are translated by the caller, which holds the directory.</summary>
internal static class DashboardMapping
{
    public static DashboardDefinitionDetail ToDetail(
        DashboardDefinition d, IReadOnlyList<DashboardAudienceRole> audience, IReadOnlyList<DashboardWidget> widgets, IReadOnlyDictionary<Guid, string> roleCodes) =>
        new(d.Id, d.Code, d.VersionNo, d.Name, d.Description, d.AllowsPersonalization, d.LifecycleState, d.ValidatedByUserId, d.ValidatedAt, d.PublishedByUserId,
            d.PublishedAt, d.RetiredAt,
            [.. audience.Select(a => new DashboardAudienceDetail(a.RoleId, roleCodes[a.RoleId], a.IsDefaultLanding)).OrderBy(a => a.RoleCode, StringComparer.Ordinal)],
            [.. widgets.Select(ToDetail)],
            d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy);

    public static DashboardWidgetDetail ToDetail(DashboardWidget w) =>
        new(w.Id, w.Code, w.Title, w.WidgetType, w.SourceProjectionCode, w.DataClassificationItemId, w.IsOptionalVisibility, w.LayoutRow, w.LayoutColumn, w.LayoutSpan);

    public static DashboardDefinitionSummary ToSummary(DashboardDefinition d) =>
        new(d.Id, d.Code, d.VersionNo, d.Name, d.LifecycleState, d.PublishedAt, d.RetiredAt, d.UpdatedAt);

    /// <summary>A stored version as content, so validation reads exactly what is stored.</summary>
    public static DashboardDefinitionContent ToContent(
        DashboardDefinition d, IReadOnlyList<DashboardAudienceRole> audience, IReadOnlyList<DashboardWidget> widgets, IReadOnlyDictionary<Guid, string> roleCodes) =>
        new(d.Name, d.Description, d.AllowsPersonalization,
            [.. audience.Select(a => new DashboardAudienceInput(roleCodes[a.RoleId], a.IsDefaultLanding)).OrderBy(a => a.RoleCode, StringComparer.Ordinal)],
            [.. widgets.Select(ToInput)]);

    public static DashboardWidgetInput ToInput(DashboardWidget w) =>
        new(w.Code, w.Title, w.WidgetType, w.SourceProjectionCode, w.DataClassificationItemId, w.IsOptionalVisibility, w.LayoutRow, w.LayoutColumn, w.LayoutSpan);

    public static DashboardWidget ToWidget(DashboardWidgetInput w, Guid definitionId, Guid actorId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            DashboardDefinitionId = definitionId,
            Code = w.Code,
            Title = w.Title,
            WidgetType = w.WidgetType,
            SourceProjectionCode = w.SourceProjectionCode,
            DataClassificationItemId = w.DataClassificationItemId,
            IsOptionalVisibility = w.IsOptionalVisibility,
            LayoutRow = w.LayoutRow,
            LayoutColumn = w.LayoutColumn,
            LayoutSpan = w.LayoutSpan,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };

    public static DashboardAudienceRole ToAudience(Guid roleId, bool isDefaultLanding, Guid definitionId, Guid actorId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            DashboardDefinitionId = definitionId,
            RoleId = roleId,
            IsDefaultLanding = isDefaultLanding,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
}
