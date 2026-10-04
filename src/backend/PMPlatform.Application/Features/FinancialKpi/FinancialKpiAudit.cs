using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;
using A = PMPlatform.Application.Features.FinancialKpi.Contracts.Events.FinancialKpiAuditAttributes;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// The audit events of WF-14 (TASK-033 <c>IAuditTrail</c>). Figures are recorded; narratives and reasons are free text and are
/// not copied. The subject is the record itself, scoped to its project.
/// </summary>
internal static class FinancialKpiAudit
{
    public const string SourceModeType = "FinancialSourceMode";
    public const string UpdateType = "FinancialProgressUpdate";
    public const string AssignmentType = "KpiAssignment";
    public const string MeasurementType = "KpiMeasurement";

    public static AuditEntry SourceModeChanged(Guid actorId, ProjectFacts project, FinancialSourceMode mode, SourceMode? before) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.SourceModeChanged, actorId, project, SourceModeType, mode.Id,
        [
            AuditAttribute.Of(A.FieldCode, mode.FieldCode),
            AuditAttribute.Change(A.SourceMode, before, mode.SourceMode)!,
        ]);

    // Versions: a commitment, a KPI target.

    public static AuditEntry VersionCreated(Guid actorId, ProjectFacts project, VersionFacts version) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.VersionCreated, actorId, project, version.Type, version.Id, [AuditAttribute.Of(A.VersionNo, version.VersionNo), .. version.Figures]);

    public static AuditEntry VersionChanged(Guid actorId, ProjectFacts project, VersionFacts before, VersionFacts after) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.VersionChanged, actorId, project, after.Type, after.Id,
            before.Figures.Zip(after.Figures, (b, a) => AuditAttribute.Change(a.Name, b.NewValue, a.NewValue)).OfType<AuditAttribute>());

    public static AuditEntry VersionDeleted(Guid actorId, ProjectFacts project, VersionFacts version) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.VersionDeleted, actorId, project, version.Type, version.Id, [AuditAttribute.Of(A.VersionNo, version.VersionNo)]);

    public static AuditEntry VersionSubmitted(Guid actorId, ProjectFacts project, VersionFacts version, ApprovedVersionStatus from, Guid approvalInstanceId) =>
        Entry(AuditEventClass.LifecycleTransition, FinancialKpiAuditEvents.VersionSubmitted, actorId, project, version.Type, version.Id,
        [
            AuditAttribute.Change(A.Status, from, ApprovedVersionStatus.Submitted)!,
            AuditAttribute.Of(A.VersionNo, version.VersionNo),
            AuditAttribute.Of(A.RevisionNo, version.RevisionNo),
            AuditAttribute.Of(A.ApprovalInstanceId, approvalInstanceId),
            .. version.Figures,
        ]);

    /// <summary>WF-11's outcome applied: activated (with the version it superseded), returned, rejected or withdrawn.</summary>
    public static AuditEntry OutcomeApplied(ProjectFacts project, VersionFacts version, ApprovedVersionStatus to, ApprovalOutcomeRecorded outcome, Guid? supersededId) =>
        Entry(AuditEventClass.LifecycleTransition, EventOf(to), outcome.Data.DecidedByUserId, project, version.Type, version.Id,
        [
            AuditAttribute.Change(A.Status, ApprovedVersionStatus.Submitted, to)!,
            AuditAttribute.Of(A.VersionNo, version.VersionNo),
            AuditAttribute.Of(A.RevisionNo, version.RevisionNo),
            AuditAttribute.Of(A.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(A.Decision, outcome.Data.Decision),
            AuditAttribute.Of(A.SupersededId, supersededId),
        ]);

    public static AuditEntry VersionSuperseded(Guid actorId, ProjectFacts project, VersionFacts version, Guid supersededById) =>
        Entry(AuditEventClass.LifecycleTransition, FinancialKpiAuditEvents.VersionSuperseded, actorId, project, version.Type, version.Id,
        [
            AuditAttribute.Change(A.Status, ApprovedVersionStatus.Active, ApprovedVersionStatus.Superseded)!,
            AuditAttribute.Of(A.VersionNo, version.VersionNo),
            AuditAttribute.Of(A.SupersededById, supersededById),
        ]);

    public static AuditEntry OutcomeIgnored(ProjectFacts project, VersionFacts version, ApprovedVersionStatus status, ApprovalOutcomeRecorded outcome) =>
        new(AuditEventClass.LifecycleTransition, FinancialKpiAuditEvents.ApprovalOutcomeIgnored, AuditOutcome.Failed)
        {
            ActorUserId = outcome.Data.DecidedByUserId,
            Subject = new AuditSubject(FinancialKpiApprovalRouting.SubjectModule, version.Type, version.Id),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes =
            [
                AuditAttribute.Of(A.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
                AuditAttribute.Of(A.Decision, outcome.Data.Decision),
                AuditAttribute.Of(A.SubjectRevisionNo, outcome.Subject.RevisionNo),
                AuditAttribute.Of(A.Status, status),
                AuditAttribute.Of(A.RevisionNo, version.RevisionNo),
            ],
        };

    public static AuditEntry DocumentAttached(Guid actorId, ProjectFacts project, FinancialCommitment commitment, EvidenceReferenceDetail evidence) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.DocumentAttached, actorId, project, FinancialKpiApprovalRouting.CommitmentType, commitment.Id, EvidenceAttributes(evidence));

    public static AuditEntry DocumentWithdrawn(Guid actorId, ProjectFacts project, FinancialCommitment commitment, EvidenceReferenceDetail evidence) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.DocumentWithdrawn, actorId, project, FinancialKpiApprovalRouting.CommitmentType, commitment.Id, EvidenceAttributes(evidence));

    // Financial updates.

    public static AuditEntry UpdateStarted(Guid actorId, ProjectFacts project, FinancialProgressUpdate update) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.UpdateStarted, actorId, project, UpdateType, update.Id,
        [
            AuditAttribute.Of(A.ReportingCycleId, update.ReportingCycleId),
            AuditAttribute.Of(A.RevisionNo, update.RevisionNo),
            AuditAttribute.Of(A.ValueStatus, update.ValueStatus),
        ]);

    public static AuditEntry UpdateChanged(Guid actorId, ProjectFacts project, UpdateFigures before, FinancialProgressUpdate update) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.UpdateChanged, actorId, project, UpdateType, update.Id,
            new[]
            {
                AuditAttribute.Change(A.ActualExpenditureToDateSar, before.Actual, update.ActualExpenditureToDateSar),
                AuditAttribute.Change(A.ForecastAtCompletionSar, before.Forecast, update.ForecastAtCompletionSar),
                AuditAttribute.Change(A.ValueStatus, before.ValueStatus, update.ValueStatus),
                AuditAttribute.Change(A.SourceReference, before.SourceReference, update.SourceReference),
                AuditAttribute.Change(A.AsOfDate, before.AsOfDate, update.AsOfDate),
                AuditAttribute.WithheldChange(A.Narrative, before.Narrative?.Text, update.Narrative?.Text),
            }.OfType<AuditAttribute>());

    public static AuditEntry UpdateDeleted(Guid actorId, ProjectFacts project, FinancialProgressUpdate update) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.UpdateDeleted, actorId, project, UpdateType, update.Id,
            [AuditAttribute.Of(A.ReportingCycleId, update.ReportingCycleId), AuditAttribute.Of(A.RevisionNo, update.RevisionNo)]);

    public static AuditEntry UpdateTransition(string eventType, Guid actorId, ProjectFacts project, FinancialProgressUpdate update, FinancialUpdateStatus from) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, UpdateType, update.Id,
        [
            AuditAttribute.Change(A.Status, from, update.Status)!,
            AuditAttribute.Of(A.ReportingCycleId, update.ReportingCycleId),
            AuditAttribute.Of(A.RevisionNo, update.RevisionNo),
            AuditAttribute.Of(A.ActualExpenditureToDateSar, update.ActualExpenditureToDateSar),
            AuditAttribute.Of(A.ForecastAtCompletionSar, update.ForecastAtCompletionSar),
            AuditAttribute.Of(A.ValueStatus, update.ValueStatus),
        ]);

    public static AuditEntry Published(Guid actorId, ProjectFacts project, FinancialProgressUpdate update, PublishedFinancialSnapshot snapshot)
    {
        AuditEntry entry = UpdateTransition(FinancialKpiAuditEvents.UpdatePublished, actorId, project, update, FinancialUpdateStatus.UnderReview);
        return entry with
        {
            Attributes =
            [
                .. entry.Attributes,
                AuditAttribute.Of(A.SnapshotId, snapshot.Id),
                AuditAttribute.Of(A.FinancialCommitmentId, snapshot.FinancialCommitmentId),
                AuditAttribute.Of(A.FinancialStatus, snapshot.FinancialStatus),
                AuditAttribute.Of(A.ThresholdConfigurationVersionId, snapshot.ThresholdConfigurationVersionId),
            ],
        };
    }

    public static AuditEntry ReviewRefused(Guid actorId, ProjectFacts project, string subjectType, Guid subjectId, string reason) =>
        new(AuditEventClass.AuthorizationDenial, FinancialKpiAuditEvents.ReviewRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(FinancialKpiApprovalRouting.SubjectModule, subjectType, subjectId),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [AuditAttribute.Of(A.Reason, reason)],
        };

    // KPI assignments and measurements.

    public static AuditEntry Assigned(Guid actorId, ProjectFacts project, KpiAssignment assignment) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.KpiAssigned, actorId, project, AssignmentType, assignment.Id,
        [
            AuditAttribute.Of(A.KpiDefinitionId, assignment.KpiDefinitionId),
            AuditAttribute.Of(A.OwnerUserId, assignment.OwnerUserId),
            AuditAttribute.Of(A.MeasurementFrequencyItemId, assignment.MeasurementFrequencyItemId),
        ]);

    public static AuditEntry AssignmentChanged(Guid actorId, ProjectFacts project, KpiAssignment assignment, Guid? ownerBefore, Guid frequencyBefore) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.AssignmentChanged, actorId, project, AssignmentType, assignment.Id,
            new[]
            {
                AuditAttribute.Change(A.OwnerUserId, ownerBefore, assignment.OwnerUserId),
                AuditAttribute.Change(A.MeasurementFrequencyItemId, frequencyBefore, assignment.MeasurementFrequencyItemId),
            }.OfType<AuditAttribute>());

    public static AuditEntry AssignmentTransitioned(Guid actorId, ProjectFacts project, KpiAssignment assignment, KpiAssignmentStatus from) =>
        Entry(AuditEventClass.LifecycleTransition, FinancialKpiAuditEvents.AssignmentTransitioned, actorId, project, AssignmentType, assignment.Id,
            [AuditAttribute.Change(A.Status, from, assignment.Status)!]);

    public static AuditEntry MeasurementRecorded(Guid actorId, ProjectFacts project, KpiMeasurement measurement) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.MeasurementRecorded, actorId, project, MeasurementType, measurement.Id, MeasurementAttributes(measurement));

    public static AuditEntry MeasurementChanged(Guid actorId, ProjectFacts project, MeasurementFigures before, KpiMeasurement measurement) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.MeasurementChanged, actorId, project, MeasurementType, measurement.Id,
            new[]
            {
                AuditAttribute.Change(A.MeasuredValue, before.Value, measurement.MeasuredValue),
                AuditAttribute.Change(A.ValueStatus, before.ValueStatus, measurement.ValueStatus),
                AuditAttribute.Change(A.RagStatus, before.RagStatus, measurement.RagStatus),
                AuditAttribute.Change(A.AsOfDate, before.AsOfDate, measurement.AsOfDate),
                AuditAttribute.WithheldChange(A.Narrative, before.Narrative?.Text, measurement.Narrative?.Text),
            }.OfType<AuditAttribute>());

    public static AuditEntry MeasurementDeleted(Guid actorId, ProjectFacts project, KpiMeasurement measurement) =>
        Entry(AuditEventClass.DataChange, FinancialKpiAuditEvents.MeasurementDeleted, actorId, project, MeasurementType, measurement.Id,
            [AuditAttribute.Of(A.PeriodStart, measurement.PeriodStart)]);

    public static AuditEntry MeasurementTransition(string eventType, Guid actorId, ProjectFacts project, KpiMeasurement measurement, KpiMeasurementStatus from) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, MeasurementType, measurement.Id,
            [AuditAttribute.Change(A.Status, from, measurement.Status)!, .. MeasurementAttributes(measurement)]);

    private static AuditAttribute[] MeasurementAttributes(KpiMeasurement m) =>
    [
        AuditAttribute.Of(A.KpiTargetVersionId, m.KpiTargetVersionId),
        AuditAttribute.Of(A.PeriodStart, m.PeriodStart),
        AuditAttribute.Of(A.MeasuredValue, m.MeasuredValue),
        AuditAttribute.Of(A.ValueStatus, m.ValueStatus),
        AuditAttribute.Of(A.RagStatus, m.RagStatus),
    ];

    private static AuditAttribute[] EvidenceAttributes(EvidenceReferenceDetail evidence) =>
    [
        AuditAttribute.Of(A.EvidenceReferenceId, evidence.Id),
        AuditAttribute.Of(A.DocumentId, evidence.DocumentId),
        AuditAttribute.Of(A.DocumentVersionId, evidence.DocumentVersionId),
    ];

    private static string EventOf(ApprovedVersionStatus to) => to switch
    {
        ApprovedVersionStatus.Active => FinancialKpiAuditEvents.VersionActivated,
        ApprovedVersionStatus.Returned => FinancialKpiAuditEvents.VersionReturned,
        ApprovedVersionStatus.Rejected => FinancialKpiAuditEvents.VersionRejected,
        ApprovedVersionStatus.Withdrawn => FinancialKpiAuditEvents.VersionWithdrawn,
        ApprovedVersionStatus.Draft or ApprovedVersionStatus.Submitted or ApprovedVersionStatus.UnderReview or ApprovedVersionStatus.Superseded or _ => throw new ArgumentOutOfRangeException(nameof(to), to, "Not an outcome of WF-11."),
    };

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, string subjectType, Guid subjectId, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(FinancialKpiApprovalRouting.SubjectModule, subjectType, subjectId),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };
}

/// <summary>A version as its audit events state it: its subject type, numbers and figures, in a fixed order so two states compare.</summary>
internal sealed record VersionFacts(string Type, Guid Id, int VersionNo, int RevisionNo, IReadOnlyList<AuditAttribute> Figures)
{
    public static VersionFacts Of(FinancialCommitment c) => new(FinancialKpiApprovalRouting.CommitmentType, c.Id, c.VersionNo, c.RevisionNo,
    [
        AuditAttribute.Of(A.AmountSar, c.AmountSar),
        AuditAttribute.Of(A.EffectiveFrom, c.EffectiveFrom),
        AuditAttribute.Of(A.SourceReference, c.SourceReference),
        AuditAttribute.Of(A.AsOfDate, c.AsOfDate),
    ]);

    public static VersionFacts Of(KpiTargetVersion t) => new(FinancialKpiApprovalRouting.TargetVersionType, t.Id, t.VersionNo, t.RevisionNo,
    [
        AuditAttribute.Of(A.TargetValue, t.TargetValue),
        AuditAttribute.Of(A.GreenThreshold, t.GreenThreshold),
        AuditAttribute.Of(A.AmberThreshold, t.AmberThreshold),
        AuditAttribute.Of(A.EffectiveFrom, t.EffectiveFrom),
    ]);
}

/// <summary>A DRAFT update's figures before a change, for its audit event.</summary>
internal sealed record UpdateFigures(Money? Actual, Money? Forecast, ValueStatus ValueStatus, string? SourceReference, DateOnly AsOfDate, NarrativeText? Narrative)
{
    public static UpdateFigures Of(FinancialProgressUpdate u) => new(u.ActualExpenditureToDateSar, u.ForecastAtCompletionSar, u.ValueStatus, u.SourceReference, u.AsOfDate, u.Narrative);
}

/// <summary>A DRAFT measurement's figure before a change, for its audit event.</summary>
internal sealed record MeasurementFigures(decimal? Value, ValueStatus ValueStatus, KpiRagStatus RagStatus, DateOnly AsOfDate, NarrativeText? Narrative)
{
    public static MeasurementFigures Of(KpiMeasurement m) => new(m.MeasuredValue, m.ValueStatus, m.RagStatus, m.AsOfDate, m.Narrative);
}
