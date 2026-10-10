using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Dashboards;

/// <summary>
/// A widget of a dashboard version: a presentation of one registered source projection (FG-01 §4.1), placed on a twelve-column
/// grid. It renders the projection's semantic state, freshness and coverage and never recomputes what the source owns (TASK-069).
/// Delete policy: CASCADE, while the version is a DRAFT.
/// </summary>
public sealed class DashboardWidget : AuditedEntity
{
    public Guid DashboardDefinitionId { get; set; }

    /// <summary>Unique within the version; a later version keeps a widget's code so personal preferences can follow it.</summary>
    public required string Code { get; set; }

    public required BilingualLabel Title { get; set; }

    public DashboardWidgetType WidgetType { get; set; }

    /// <summary>The registered projection's code, <c>&lt;SOURCE&gt;.&lt;PROJECTION&gt;</c>; anything else is refused at validation (BR-DSH-029).</summary>
    public required string SourceProjectionCode { get; set; }

    /// <summary>A DATA_CLASSIFICATION item a viewer must be cleared for to receive the widget's data (ADR-010); null when unclassified.</summary>
    public Guid? DataClassificationItemId { get; set; }

    /// <summary>ADR-019: whether a user of a personalisable dashboard may hide or reorder it.</summary>
    public bool IsOptionalVisibility { get; set; }

    /// <summary>From 1.</summary>
    public short LayoutRow { get; set; }

    /// <summary>1 to 12.</summary>
    public short LayoutColumn { get; set; }

    /// <summary>1 to 12, and the widget ends by column 12.</summary>
    public short LayoutSpan { get; set; } = 1;
}
